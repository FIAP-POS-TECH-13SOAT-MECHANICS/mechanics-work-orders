using Mechanics.Application.WorkOrders.Services;
using Mechanics.Infra.Messaging.Consumers;
using Microsoft.Extensions.Logging;

namespace Mechanics.Application.WorkOrders.Consumers;

public class WorkOrderStatusChangedConsumer(
    ILogger<WorkOrderStatusChangedConsumer> logger,
    WorkOrderAppService service) : IEventConsumer<WorkOrderStatusChangedEvent>
{
    public async Task ConsumeAsync(WorkOrderStatusChangedEvent message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Processing work-order-status-changed for '{WorkOrderId}' ({OldStatus} -> {NewStatus})",
            message.WorkOrderId,
            message.OldStatus,
            message.NewStatus);

        var updated = await service.ApplyStatusChangedEvent(message, cancellationToken);

        logger.LogInformation(
            updated
                ? "Work order '{WorkOrderId}' synchronized to status '{NewStatus}'"
                : "Work order '{WorkOrderId}' ignored duplicated status '{NewStatus}'",
            message.WorkOrderId,
            message.NewStatus);
    }
}
