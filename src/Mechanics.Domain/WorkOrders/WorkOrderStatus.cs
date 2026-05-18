namespace Mechanics.Domain.WorkOrders;

public enum WorkOrderStatus
{
    Received = 0,
    UnderDiagnosis = 1,
    PendingApproval = 2,
    InProgress = 3,
    Completed = 4,
    Delivered = 5,
    ReadyForDelivery = 6,
}
