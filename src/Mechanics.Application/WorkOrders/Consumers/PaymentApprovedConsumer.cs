using Mechanics.Application.WorkOrders.Services;
using Mechanics.Infra.Messaging.Consumers;
using Microsoft.Extensions.Logging;

namespace Mechanics.Application.WorkOrders.Consumers;

public class PaymentApprovedConsumer(
    ILogger<PaymentApprovedConsumer> logger,
    WorkOrderAppService service) : IEventConsumer<PaymentApprovedEvent>
{
    public async Task ConsumeAsync(PaymentApprovedEvent message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing payment-approved for '{WorkOrderId}'", message.WorkOrderId);

        var updated = await service.ApplyPaymentApprovedEvent(message, cancellationToken);

        logger.LogInformation(
            updated
                ? "Work order '{WorkOrderId}' marked as paid"
                : "Work order '{WorkOrderId}' already marked as paid",
            message.WorkOrderId);
    }
}
