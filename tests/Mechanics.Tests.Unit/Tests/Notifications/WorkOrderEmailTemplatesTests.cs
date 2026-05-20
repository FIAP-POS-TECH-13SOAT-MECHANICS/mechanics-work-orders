using Mechanics.Application.Notification.Templates;
using Mechanics.Domain.Customers;
using Mechanics.Domain.WorkOrders;

namespace Mechanics.Tests.Unit.Tests.Notifications;

[TestClass]
[TestCategory("Notifications")]
public class WorkOrderEmailTemplatesTests
{
    [TestMethod("Template de criação deve preencher assunto, destinatário e chave formatada")]
    public void It_ShouldBuildCreatedTemplate()
    {
        var customer = CreateCustomer();
        var workOrder = CreateWorkOrder(status: WorkOrderStatus.Received, reportedProblem: "Motor falhando");

        var message = WorkOrderEmailTemplates.WorkOrderCreated(customer, workOrder);

        Assert.AreEqual(customer.Email, message.Recipient);
        Assert.AreEqual("Ordem de serviço criada - FIAP Mechanics", message.Subject);
        Assert.IsTrue(message.Body.Contains(customer.Name));
        Assert.IsTrue(message.Body.Contains("Motor falhando"));
        Assert.IsTrue(message.Body.Contains("1234 5678"));
    }

    [TestMethod("Template de criação deve usar placeholder quando problema não for informado")]
    public void It_ShouldBuildCreatedTemplateWithFallbackProblem()
    {
        var customer = CreateCustomer();
        var workOrder = CreateWorkOrder(status: WorkOrderStatus.Received, reportedProblem: null);

        var message = WorkOrderEmailTemplates.WorkOrderCreated(customer, workOrder);

        Assert.IsTrue(message.Body.Contains("—"));
    }

    [TestMethod("Template de mudança de status deve traduzir status atual e anterior")]
    public void It_ShouldBuildStatusChangedTemplate()
    {
        var customer = CreateCustomer();
        var workOrder = CreateWorkOrder(status: WorkOrderStatus.InProgress);

        var message = WorkOrderEmailTemplates.WorkOrderStatusChanged(customer, workOrder, WorkOrderStatus.PendingApproval);

        Assert.AreEqual(customer.Email, message.Recipient);
        Assert.IsTrue(message.Subject.Contains("Em execução"));
        Assert.IsTrue(message.Body.Contains("Aguardando aprovação"));
        Assert.IsTrue(message.Body.Contains("Em execução"));
    }

    [TestMethod("Template de mudança de status deve lançar para status atual inválido")]
    public void It_ShouldThrow_WhenCurrentStatusIsInvalid()
    {
        var customer = CreateCustomer();
        var workOrder = CreateWorkOrder(status: (WorkOrderStatus)999);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            WorkOrderEmailTemplates.WorkOrderStatusChanged(customer, workOrder, WorkOrderStatus.Received));
    }

    [TestMethod("Template de mudança de status deve lançar para status anterior inválido")]
    public void It_ShouldThrow_WhenPreviousStatusIsInvalid()
    {
        var customer = CreateCustomer();
        var workOrder = CreateWorkOrder(status: WorkOrderStatus.Completed);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            WorkOrderEmailTemplates.WorkOrderStatusChanged(customer, workOrder, (WorkOrderStatus)999));
    }

    [TestMethod("Template de cancelamento deve conter assunto e destinatário")]
    public void It_ShouldBuildCancelledTemplate()
    {
        var customer = CreateCustomer();
        var workOrder = CreateWorkOrder(status: WorkOrderStatus.Received);

        var message = WorkOrderEmailTemplates.WorkOrderCancelled(customer, workOrder);

        Assert.AreEqual(customer.Email, message.Recipient);
        Assert.AreEqual("OS 12345678 cancelada - FIAP Mechanics", message.Subject);
        Assert.IsTrue(message.Body.Contains("foi cancelada"));
    }

    [TestMethod("Template de pesquisa pós-entrega deve conter assunto e conteúdo esperado")]
    public void It_ShouldBuildDeliveredSurveyTemplate()
    {
        var customer = CreateCustomer();
        var workOrder = CreateWorkOrder(status: WorkOrderStatus.Delivered);

        var message = WorkOrderEmailTemplates.WorkOrderDeliveredSurvey(customer, workOrder);

        Assert.AreEqual(customer.Email, message.Recipient);
        Assert.AreEqual("Pesquisa de satisfação - Ordem 12345678", message.Subject);
        Assert.IsTrue(message.Body.Contains("Seu veículo foi entregue."));
    }

    private static Customer CreateCustomer() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Joao da Silva",
        Email = "joao@teste.com",
        Document = new PersonalDocument(DocumentType.Cpf, "11144477735"),
    };

    private static WorkOrder CreateWorkOrder(WorkOrderStatus status, string? reportedProblem = "Freios")
    {
        return new WorkOrder
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            AccessKey = "12345678",
            CreationDate = new DateTime(2026, 5, 20, 10, 30, 0),
            Status = status,
            LastUpdate = new DateTime(2026, 5, 20, 10, 30, 0),
            ReportedProblem = reportedProblem,
        };
    }
}
