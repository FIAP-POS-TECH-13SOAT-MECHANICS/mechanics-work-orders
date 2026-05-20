using Mechanics.Application.Notification.Services;
using Mechanics.Domain.Customers;
using Mechanics.Domain.WorkOrders;
using Mechanics.Infra.Integrations.EmailSender;
using Microsoft.Extensions.Logging;
using Moq;

namespace Mechanics.Tests.Unit.Tests.Notifications;

[TestClass]
[TestCategory("Notifications")]
public class EmailServiceTests
{
    [TestMethod("Envia e-mail de criação de OS")]
    public async Task It_ShouldSendWorkOrderCreatedEmail()
    {
        var logger = new Mock<ILogger<EmailService>>();
        var sender = new Mock<IEmailSenderService>();
        var service = new EmailService(logger.Object, sender.Object);
        var customer = CreateCustomer();
        var workOrder = CreateWorkOrder(WorkOrderStatus.Received);

        await service.SendWorkOrderCreated(customer, workOrder);

        sender.Verify(mock => mock.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == customer.Email &&
                    message.Subject == "Ordem de serviço criada - FIAP Mechanics"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod("Envia e-mail de mudança de status de OS")]
    public async Task It_ShouldSendWorkOrderStatusChangedEmail()
    {
        var logger = new Mock<ILogger<EmailService>>();
        var sender = new Mock<IEmailSenderService>();
        var service = new EmailService(logger.Object, sender.Object);
        var customer = CreateCustomer();
        var workOrder = CreateWorkOrder(WorkOrderStatus.InProgress);

        await service.SendWorkOrderStatusChanged(customer, workOrder, WorkOrderStatus.PendingApproval);

        sender.Verify(mock => mock.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == customer.Email &&
                    message.Subject.Contains("Em execução")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod("Envia e-mail de cancelamento de OS")]
    public async Task It_ShouldSendWorkOrderCancelledEmail()
    {
        var logger = new Mock<ILogger<EmailService>>();
        var sender = new Mock<IEmailSenderService>();
        var service = new EmailService(logger.Object, sender.Object);
        var customer = CreateCustomer();
        var workOrder = CreateWorkOrder(WorkOrderStatus.Received);

        await service.SendWorkOrderCancelled(customer, workOrder);

        sender.Verify(mock => mock.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == customer.Email &&
                    message.Subject.Contains("cancelada")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod("Envia e-mail de pesquisa pós-entrega de OS")]
    public async Task It_ShouldSendWorkOrderDeliveredSurveyEmail()
    {
        var logger = new Mock<ILogger<EmailService>>();
        var sender = new Mock<IEmailSenderService>();
        var service = new EmailService(logger.Object, sender.Object);
        var customer = CreateCustomer();
        var workOrder = CreateWorkOrder(WorkOrderStatus.Delivered);

        await service.SendWorkOrderDeliveredSurvey(customer, workOrder);

        sender.Verify(mock => mock.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == customer.Email &&
                    message.Subject.Contains("Pesquisa de satisfação")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Customer CreateCustomer() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Joao da Silva",
        Email = "joao@teste.com",
        Document = new PersonalDocument(DocumentType.Cpf, "11144477735"),
    };

    private static WorkOrder CreateWorkOrder(WorkOrderStatus status) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        VehicleId = Guid.NewGuid(),
        AccessKey = "12345678",
        CreationDate = new DateTime(2026, 5, 20, 10, 30, 0),
        LastUpdate = new DateTime(2026, 5, 20, 10, 30, 0),
        Status = status,
        ReportedProblem = "Freios",
    };
}
