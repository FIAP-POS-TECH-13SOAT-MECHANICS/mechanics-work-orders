using AutoMapper;
using Mechanics.Application.Identity.Services;
using Mechanics.Application.Notification.Services;
using Mechanics.Application.Observability;
using Mechanics.Application.Utils;
using Mechanics.Application.Utils.CommonResponses;
using Mechanics.Application.Utils.PagedList;
using Mechanics.Application.WorkOrders.Consumers;
using Mechanics.Application.WorkOrders.Events;
using Mechanics.Application.WorkOrders.Requests;
using Mechanics.Application.WorkOrders.Responses;
using Mechanics.Domain.Auth;
using Mechanics.Domain.Base.Exceptions;
using Mechanics.Domain.WorkOrders;
using Mechanics.Infra.Data;
using Mechanics.Infra.Messaging.Publishers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Mechanics.Application.WorkOrders.Services;

public class WorkOrderAppService(
    AppDbContext db,
    IMapper mapper,
    IEmailService emailService,
    IEventPublisher eventPublisher,
    IUserService userService,
    ILogger<WorkOrderAppService> logger)
    : IAppService
{
    /// <summary>
    ///     Cria uma nova WorkOrder.
    /// </summary>
    public async Task<CreateItemResponse> Create(CreateWorkOrderRequest request, Guid createdByUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var vehicle = await db.Vehicles
                .Include(v => v.Owner)
                .FirstOrDefaultAsync(v => v.Id == request.VehicleId, cancellationToken);

            EntityNotFoundException.ThrowIfNull(vehicle, request.VehicleId);
            EntityNotFoundException.ThrowIfNull(vehicle.Owner, vehicle.OwnerId);

            var existingOrders = await db.WorkOrders
                .Where(workOrder => workOrder.CustomerId == vehicle.OwnerId)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var workOrder = new WorkOrder
            {
                CustomerId = vehicle.OwnerId,
                VehicleId = request.VehicleId,
                AccessKey = WorkOrder.GenerateNewAccessKey(existingOrders),
                Status = WorkOrderStatus.Received,
                LastUpdate = now,
                CreatedByUserId = createdByUserId,
                ReportedProblem = request.ReportedProblem,
                Observations = request.Observations,
            };

            await db.WorkOrders.AddAsync(workOrder, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await db.WorkOrderHistories.AddAsync(new WorkOrderHistory
            {
                WorkOrderId = workOrder.Id,
                Action = "Created",
                Details = "Work order created",
                PerformedByUserId = createdByUserId,
            }, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            var workOrderCreatedEvent = new WorkOrderCreatedEvent
            {
                EventId = Guid.NewGuid(),
                OccurredAt = now,
                WorkOrderId = workOrder.Id,
                CustomerId = workOrder.CustomerId,
                VehicleId = workOrder.VehicleId,
                Status = workOrder.Status.ToString(),
                CreatedByUserId = createdByUserId,
                ReportedProblem = workOrder.ReportedProblem,
            };

            try
            {
                await eventPublisher.PublishAsync(workOrderCreatedEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Work order {WorkOrderId} was committed, but publishing {EventName} failed. EventId: {EventId}. " +
                    "Message delivery should be recovered by a retry/outbox flow.",
                    workOrder.Id,
                    nameof(WorkOrderCreatedEvent),
                    workOrderCreatedEvent.EventId);
            }

            AppMetrics.WorkOrdersCreated.Add(1, new TagList
            {
                { "status", "created" },
            });

            logger.LogInformation(
                "Work order created | {work_order.id} | {vehicle.id} | {work_order.status}",
                workOrder.Id,
                workOrder.VehicleId,
                workOrder.Status.ToString());

            try
            {
                await emailService.SendWorkOrderCreated(vehicle.Owner!, workOrder, cancellationToken);
                AppMetrics.EmailsSent.Add(1, new TagList { { "template", "work_order_created" } });
            }
            catch (Exception emailEx)
            {
                AppMetrics.EmailsFailed.Add(1, new TagList { { "template", "work_order_created" } });
                logger.LogWarning(emailEx, "Failed to send WorkOrder created email for {WorkOrderId}", workOrder.Id);
            }

            return new CreateItemResponse { CreatedId = workOrder.Id };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while creating work order");
            throw;
        }
    }

    public async Task Assign(Guid workOrderId, Guid assignedToUserId, Guid performedByUserId, string? comment = null,
        CancellationToken cancellationToken = default)
    {
        var workOrder = await db.WorkOrders.FirstOrDefaultAsync(item => item.Id == workOrderId, cancellationToken);
        EntityNotFoundException.ThrowIfNull(workOrder, workOrderId);

        var assignedUser = await userService.GetUserById(assignedToUserId, cancellationToken);
        if (assignedUser is null)
            throw new BusinessException($"User with key '{assignedToUserId}' not found.");

        if (!string.Equals(assignedUser.Role.Name, RoleNames.Mechanic, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("Assigned user must be a mechanic.");

        workOrder.AssignedToUserId = assignedToUserId;
        workOrder.LastUpdate = DateTime.UtcNow;

        await db.WorkOrderHistories.AddAsync(new WorkOrderHistory
        {
            WorkOrderId = workOrder.Id,
            Action = "Assigned",
            Details = comment is null
                ? $"Assigned to {assignedToUserId}"
                : $"Assigned to {assignedToUserId}. Comment: {comment}",
            PerformedByUserId = performedByUserId,
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        if (workOrder.Status == WorkOrderStatus.Received)
        {
            await ChangeStatus(
                workOrderId,
                WorkOrderStatus.UnderDiagnosis,
                performedByUserId,
                "Auto-transition to UnderDiagnosis due assignment",
                cancellationToken);
        }
    }

    /// <summary>
    ///     Obtém detalhes de uma WorkOrder por id.
    /// </summary>
    public async Task<GetWorkOrderResponse?> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var workOrder = await db.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return workOrder is null ? null : mapper.Map<GetWorkOrderResponse>(workOrder);
    }

    /// <summary>
    ///     Lista WorkOrders com filtros opcionais e paginação.
    /// </summary>
    public async Task<GetWorkOrdersResponse> GetList(GetWorkOrdersRequest request,
        CancellationToken cancellationToken = default)
    {
        var hasCustomer = request.CustomerId.HasValue;
        var hasVehicle = request.VehicleId.HasValue;

        var query = db.WorkOrders
            .AsNoTracking()
            .Where(item => !hasCustomer || item.CustomerId == request.CustomerId!.Value)
            .Where(item => !hasVehicle || item.VehicleId == request.VehicleId!.Value)
            .Where(item => request.IncludeCompleted ||
                item.Status != WorkOrderStatus.Completed &&
                item.Status != WorkOrderStatus.ReadyForDelivery &&
                item.Status != WorkOrderStatus.Delivered)
            .OrderByDescending(item => item.Status)
            .ThenBy(item => item.CreationDate);

        var (items, count) = await query.GetPaginatedList(request, cancellationToken);
        return new GetWorkOrdersResponse(mapper.Map<IEnumerable<GetWorkOrderResponse>>(items), count);
    }

    /// <summary>
    ///     Consulta pela accessKey.
    /// </summary>
    /// <remarks>Usado pelo cliente para acompanhar o progresso da OS.</remarks>
    public async Task<GetWorkOrderResponse?> TrackByAccessKey(Guid customerId, string accessKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedAccessKey = accessKey.Replace(" ", string.Empty);

        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);

        EntityNotFoundException.ThrowIfNull(customer, customerId);

        var workOrder = await db.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.CustomerId == customer.Id && item.AccessKey == normalizedAccessKey,
                cancellationToken);

        return workOrder is null ? null : mapper.Map<GetWorkOrderResponse>(workOrder);
    }

    public async Task<bool> ChangeStatus(Guid workOrderId, WorkOrderStatus newStatus, Guid performedByUserId,
        string? comment = null, CancellationToken cancellationToken = default)
    {
        var workOrder = await db.WorkOrders.FirstOrDefaultAsync(item => item.Id == workOrderId, cancellationToken);
        EntityNotFoundException.ThrowIfNull(workOrder, workOrderId);

        var previousStatus = workOrder.Status;
        var statusAfterTransition = (newStatus == WorkOrderStatus.Completed && workOrder.PaidAt.HasValue)
            ? WorkOrderStatus.ReadyForDelivery
            : newStatus;

        if (previousStatus == statusAfterTransition)
        {
            logger.LogInformation("Work order {WorkOrderId} already in status {Status}. Ignoring.", workOrder.Id, statusAfterTransition);
            return false;
        }

        if (newStatus == WorkOrderStatus.Delivered && !workOrder.PaidAt.HasValue)
            throw new BusinessException("Work order must be paid before delivery.");

        if (!IsTransitionAllowed(previousStatus, newStatus))
            throw new BusinessException($"Invalid status transition from {previousStatus} to {newStatus}.");

        var timeInPreviousStatus = DateTime.UtcNow - workOrder.LastUpdate;

        workOrder.Status = statusAfterTransition;
        workOrder.LastStatusChangedByUserId = performedByUserId;
        workOrder.LastUpdate = DateTime.UtcNow;
        if (statusAfterTransition == WorkOrderStatus.Delivered && workOrder.DeliveredAt is null)
            workOrder.DeliveredAt = DateTime.UtcNow;

        await db.WorkOrderHistories.AddAsync(new WorkOrderHistory
        {
            WorkOrderId = workOrder.Id,
            Action = "StatusChanged",
            Details = BuildStatusChangedDetails(previousStatus, newStatus, statusAfterTransition, comment),
            PerformedByUserId = performedByUserId,
        }, cancellationToken);

        var tags = new TagList
        {
            { "previous_status", previousStatus.ToString() },
            { "new_status", statusAfterTransition.ToString() },
        };

        AppMetrics.TimeInStatusTotalSeconds.Add(Math.Round(timeInPreviousStatus.TotalSeconds, 2), tags);
        AppMetrics.TimeInStatusSamples.Add(1, tags);
        AppMetrics.StatusTransitions.Add(1, tags);
        AppMetrics.StatusDurationSeconds.Record(
            Math.Round(timeInPreviousStatus.TotalSeconds, 2),
            new TagList { { "status", previousStatus.ToString() } });

        await db.SaveChangesAsync(cancellationToken);

        var customer = await db.Customers.FindAsync([workOrder.CustomerId], cancellationToken);
        if (customer is null)
            return true;

        try
        {
            await emailService.SendWorkOrderStatusChanged(customer, workOrder, previousStatus, cancellationToken);
            AppMetrics.EmailsSent.Add(1, new TagList { { "template", "status_changed" } });

            if (statusAfterTransition == WorkOrderStatus.Delivered)
            {
                await emailService.SendWorkOrderDeliveredSurvey(customer, workOrder, cancellationToken);
                AppMetrics.EmailsSent.Add(1, new TagList { { "template", "delivered_survey" } });
            }
        }
        catch (Exception ex)
        {
            AppMetrics.EmailsFailed.Add(1, new TagList { { "template", "status_changed" } });
            logger.LogWarning(ex, "Failed to send status email notifications for WorkOrder {WorkOrderId}", workOrder.Id);
        }

        return true;
    }

    public async Task<bool> ApplyStatusChangedEvent(WorkOrderStatusChangedEvent message,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<WorkOrderStatus>(message.NewStatus, true, out var newStatus))
            throw new BusinessException($"Invalid status value '{message.NewStatus}'.");

        var currentWorkOrder = await db.WorkOrders.FirstOrDefaultAsync(item => item.Id == message.WorkOrderId, cancellationToken);
        EntityNotFoundException.ThrowIfNull(currentWorkOrder, message.WorkOrderId);

        var changed = await ChangeStatus(
            message.WorkOrderId,
            newStatus,
            message.LastStatusChangeBy,
            $"Sync from work-order-status-changed event ({message.OldStatus} -> {message.NewStatus})",
            cancellationToken);

        currentWorkOrder.LastUpdate = message.LastUpdate.UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);

        return changed;
    }

    public async Task<bool> ApplyPaymentApprovedEvent(PaymentApprovedEvent message,
        CancellationToken cancellationToken = default)
    {
        var workOrder = await db.WorkOrders.FirstOrDefaultAsync(item => item.Id == message.WorkOrderId, cancellationToken);
        EntityNotFoundException.ThrowIfNull(workOrder, message.WorkOrderId);

        if (message.PaidAt == default)
            throw new BusinessException("Invalid payment-approved event: PaidAt is required.");

        if (workOrder.PaidAt.HasValue)
        {
            logger.LogInformation("Work order {WorkOrderId} already has approved payment. Ignoring.", workOrder.Id);
            return false;
        }

        workOrder.PaidAt = message.PaidAt;
        workOrder.LastUpdate = DateTime.UtcNow;
        var promotedToReadyForDelivery = workOrder.Status == WorkOrderStatus.Completed;
        if (promotedToReadyForDelivery)
            workOrder.Status = WorkOrderStatus.ReadyForDelivery;

        await db.WorkOrderHistories.AddAsync(new WorkOrderHistory
        {
            WorkOrderId = workOrder.Id,
            Action = "PaymentApproved",
            Details = $"Payment approved at {message.PaidAt:O}. PromotedToReadyForDelivery: {promotedToReadyForDelivery}",
        }, cancellationToken);

        if (promotedToReadyForDelivery)
        {
            await db.WorkOrderHistories.AddAsync(new WorkOrderHistory
            {
                WorkOrderId = workOrder.Id,
                Action = "StatusChanged",
                Details = $"Sync from payment-approved event. From {WorkOrderStatus.Completed} to {WorkOrderStatus.ReadyForDelivery}.",
            }, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateDetails(Guid workOrderId, UpdateWorkOrderRequest request, Guid performedByUserId,
        CancellationToken cancellationToken = default)
    {
        var workOrder = await db.WorkOrders.FirstOrDefaultAsync(item => item.Id == workOrderId, cancellationToken);
        EntityNotFoundException.ThrowIfNull(workOrder, workOrderId);

        if (request.Observations is null || request.Observations == workOrder.Observations)
            return;

        workOrder.Observations = request.Observations;
        workOrder.LastUpdate = DateTime.UtcNow;

        await db.WorkOrderHistories.AddAsync(new WorkOrderHistory
        {
            WorkOrderId = workOrder.Id,
            Action = "DetailsUpdated",
            Details = "ObservationsUpdated",
            PerformedByUserId = performedByUserId,
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsTransitionAllowed(WorkOrderStatus from, WorkOrderStatus to) =>
        (from, to) switch
        {
            (WorkOrderStatus.Received, WorkOrderStatus.UnderDiagnosis) => true,
            (WorkOrderStatus.UnderDiagnosis, WorkOrderStatus.PendingApproval) => true,
            (WorkOrderStatus.PendingApproval, WorkOrderStatus.UnderDiagnosis) => true,
            (WorkOrderStatus.PendingApproval, WorkOrderStatus.InProgress) => true,
            (WorkOrderStatus.InProgress, WorkOrderStatus.Completed) => true,
            (WorkOrderStatus.Completed, WorkOrderStatus.ReadyForDelivery) => true,
            (WorkOrderStatus.ReadyForDelivery, WorkOrderStatus.Delivered) => true,
            _ => false,
        };

    private static string BuildStatusChangedDetails(
        WorkOrderStatus previousStatus,
        WorkOrderStatus requestedStatus,
        WorkOrderStatus finalStatus,
        string? comment)
    {
        if (requestedStatus == finalStatus)
            return comment is null
                ? $"From {previousStatus} to {requestedStatus}"
                : $"From {previousStatus} to {requestedStatus}. Comment: {comment}";

        return comment is null
            ? $"From {previousStatus} to {requestedStatus}. Auto-transition to {finalStatus} due approved payment."
            : $"From {previousStatus} to {requestedStatus}. Auto-transition to {finalStatus} due approved payment. Comment: {comment}";
    }
}
