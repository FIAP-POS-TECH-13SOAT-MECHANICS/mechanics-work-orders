namespace Mechanics.Application.WorkOrders.Consumers;

public class PaymentApprovedEvent
{
    public required Guid WorkOrderId { get; init; }
    public required DateTimeOffset PaidAt { get; init; }
}
