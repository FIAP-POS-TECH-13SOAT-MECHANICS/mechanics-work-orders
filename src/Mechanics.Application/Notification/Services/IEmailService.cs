using Mechanics.Domain.Customers;
using Mechanics.Domain.WorkOrders;

namespace Mechanics.Application.Notification.Services;

public interface IEmailService
{
    Task SendWorkOrderCreated(Customer customer, WorkOrder workOrder, CancellationToken cancellationToken = default);

    Task SendWorkOrderStatusChanged(Customer customer, WorkOrder workOrder, WorkOrderStatus previousStatus,
        CancellationToken cancellationToken = default);

    Task SendWorkOrderCancelled(Customer customer, WorkOrder workOrder, CancellationToken cancellationToken = default);

    Task SendWorkOrderDeliveredSurvey(Customer customer, WorkOrder workOrder, CancellationToken cancellationToken = default);
}
