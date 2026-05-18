using Mechanics.Application.Utils.PagedList;
using Mechanics.Domain.WorkOrders;

namespace Mechanics.Application.WorkOrders.Responses;

public class GetWorkOrdersResponse(IEnumerable<GetWorkOrderResponse> items, int totalCount)
    : PaginatedListResponse<GetWorkOrderResponse>(items, totalCount);

public class GetWorkOrderResponse
{
    public required Guid Id { get; init; }
    public required string AccessKey { get; init; }
    public required WorkOrderStatus Status { get; init; }
    public required DateTime CreationDate { get; init; }
    public DateTime? LastUpdate { get; init; }
    public Guid CustomerId { get; init; }
    public Guid VehicleId { get; init; }

    public string? ReportedProblem { get; init; }
    public string? Observations { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public Guid? LastStatusChangedByUserId { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
    public bool IsPaymentApproved { get; init; }
    public bool IsReadyForDelivery { get; init; }
}
