using AutoMapper;
using Mechanics.Application.Identity.Responses;
using Mechanics.Application.Identity.Services;
using Mechanics.Application.Notification.Services;
using Mechanics.Application.WorkOrders.Consumers;
using Mechanics.Application.WorkOrders.Events;
using Mechanics.Application.WorkOrders.Requests;
using Mechanics.Application.WorkOrders.Services;
using Mechanics.Domain.Base.Exceptions;
using Mechanics.Domain.WorkOrders;
using Mechanics.Infra.Messaging.Publishers;
using Mechanics.Tests.Unit.Helpers;
using Mechanics.Tests.Unit.Mocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mechanics.Tests.Unit.Tests.WorkOrders;

[TestClass]
[TestCategory("WorkOrders")]
public class WorkOrderAppServiceTests
{
    public TestContext TestContext { get; set; } = null!;

    private readonly IMapper _mapper = AutoMapperFactory.CreateMap("WorkOrders");
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<IEventPublisher> _eventPublisherMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();

    [TestInitialize]
    public void Setup()
    {
        _emailServiceMock.Reset();
        _eventPublisherMock.Reset();
        _userServiceMock.Reset();

        _emailServiceMock
            .Setup(service => service.SendWorkOrderCreated(It.IsAny<Mechanics.Domain.Customers.Customer>(), It.IsAny<WorkOrder>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _emailServiceMock
            .Setup(service => service.SendWorkOrderStatusChanged(It.IsAny<Mechanics.Domain.Customers.Customer>(), It.IsAny<WorkOrder>(),
                It.IsAny<WorkOrderStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _emailServiceMock
            .Setup(service => service.SendWorkOrderDeliveredSurvey(It.IsAny<Mechanics.Domain.Customers.Customer>(), It.IsAny<WorkOrder>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [TestMethod("Cria OS com Vehicle, Customer derivado, problema e observações")]
    public async Task It_ShouldCreateWorkOrder()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var request = new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Barulho no motor",
            Observations = "Cliente informou ruído ao acelerar",
        };

        var response = await service.Create(request, Guid.NewGuid(), CancellationToken.None);

        var created = await context.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == response.CreatedId, TestContext.CancellationTokenSource.Token);

        Assert.IsNotNull(created);
        Assert.AreEqual(vehicleId, created.VehicleId);
        Assert.AreEqual(customerId, created.CustomerId);
        Assert.AreEqual("Barulho no motor", created.ReportedProblem);
        Assert.AreEqual("Cliente informou ruído ao acelerar", created.Observations);
        Assert.AreEqual(WorkOrderStatus.Received, created.Status);
    }

    [TestMethod("Falha ao criar OS quando veículo não existe")]
    public async Task It_ShouldThrow_WhenVehicleDoesNotExist()
    {
        await using var context = new DbContextTestBuilder().Build();
        var service = BuildService(context);

        var request = new CreateWorkOrderRequest
        {
            VehicleId = Guid.NewGuid(),
            ReportedProblem = "Sem partida",
            Observations = "Sem observações",
        };

        await Assert.ThrowsExactlyAsync<EntityNotFoundException>(async () =>
            await service.Create(request, Guid.NewGuid(), CancellationToken.None));
    }

    [TestMethod("Falha ao criar OS quando customer dono do veículo não existe")]
    public async Task It_ShouldThrow_WhenCustomerDoesNotExist()
    {
        var ownerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([VehicleMocks.CreateVehicle(vehicleId, ownerId)])
            .Build();

        var service = BuildService(context);
        var request = new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Falha elétrica",
            Observations = "Cliente relata intermitência",
        };

        await Assert.ThrowsExactlyAsync<EntityNotFoundException>(async () =>
            await service.Create(request, Guid.NewGuid(), CancellationToken.None));
    }

    [TestMethod("Cria OS sem Product, ServiceCatalog e Budget")]
    public async Task It_ShouldCreateWorkOrder_WithoutProductServiceCatalogBudget()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);

        var response = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Luz de injeção acesa",
            Observations = null,
        }, Guid.NewGuid(), CancellationToken.None);

        Assert.AreNotEqual(Guid.Empty, response.CreatedId);
    }

    [TestMethod("Publica WorkOrderCreatedEvent após criar OS")]
    public async Task It_ShouldPublishWorkOrderCreatedEvent()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);

        await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Superaquecimento",
            Observations = "Verificar radiador",
        }, Guid.NewGuid(), CancellationToken.None);

        _eventPublisherMock.Verify(
            publisher => publisher.PublishAsync(It.IsAny<WorkOrderCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod("Criação de OS não deve falhar se publicação de evento falhar")]
    public async Task It_ShouldCreateWorkOrder_WhenEventPublishFails()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        _eventPublisherMock
            .Setup(publisher => publisher.PublishAsync(It.IsAny<WorkOrderCreatedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SQS unavailable"));

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);

        var response = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Falha intermitente",
            Observations = "Teste de resiliência de publish",
        }, Guid.NewGuid(), CancellationToken.None);

        var created = await context.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == response.CreatedId, TestContext.CancellationTokenSource.Token);
        var history = await context.WorkOrderHistories
            .AsNoTracking()
            .Where(item => item.WorkOrderId == response.CreatedId && item.Action == "Created")
            .ToListAsync(TestContext.CancellationTokenSource.Token);

        Assert.IsNotNull(created);
        Assert.AreEqual(1, history.Count);
    }

    [TestMethod("Consulta OS por Id")]
    public async Task It_ShouldGetWorkOrderById()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var createResponse = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Freio baixo",
            Observations = "Checar pastilhas",
        }, createdBy, CancellationToken.None);

        var response = await service.Get(createResponse.CreatedId, TestContext.CancellationTokenSource.Token);

        Assert.IsNotNull(response);
        Assert.AreEqual(createResponse.CreatedId, response.Id);
        Assert.AreEqual(vehicleId, response.VehicleId);
        Assert.AreEqual(customerId, response.CustomerId);
        Assert.AreEqual(createdBy, response.CreatedByUserId);
    }

    [TestMethod("Lista OSs")]
    public async Task It_ShouldListWorkOrders()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Vibração",
            Observations = "Volante trepidando",
        }, Guid.NewGuid(), CancellationToken.None);

        var response = await service.GetList(new GetWorkOrdersRequest
        {
            Page = 1,
            ItemsPerPage = 10,
            IncludeCompleted = true,
        }, TestContext.CancellationTokenSource.Token);

        Assert.IsNotNull(response);
        Assert.AreEqual(1, response.TotalCount);
        Assert.AreEqual(1, response.Items.Count());
    }

    [TestMethod("Atualiza apenas campos locais permitidos")]
    public async Task It_ShouldUpdateOnlyLocalFields()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var performerId = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var createResponse = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Ruído em baixa",
            Observations = "Obs inicial",
        }, Guid.NewGuid(), CancellationToken.None);

        await service.UpdateDetails(createResponse.CreatedId, new UpdateWorkOrderRequest
        {
            Observations = "Obs atualizada",
        }, performerId, CancellationToken.None);

        var updated = await context.WorkOrders
            .AsNoTracking()
            .FirstAsync(item => item.Id == createResponse.CreatedId, TestContext.CancellationTokenSource.Token);
        var history = await context.WorkOrderHistories
            .AsNoTracking()
            .Where(item => item.WorkOrderId == createResponse.CreatedId && item.Action == "DetailsUpdated")
            .ToListAsync(TestContext.CancellationTokenSource.Token);

        Assert.AreEqual("Obs atualizada", updated.Observations);
        Assert.AreEqual(1, history.Count);
    }

    [TestMethod("Atribui AssignedToUserId como referência externa")]
    public async Task It_ShouldAssignExternalUserId()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var performerId = Guid.NewGuid();

        _userServiceMock
            .Setup(service => service.GetUserById(assigneeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse
            {
                Id = assigneeId,
                FullName = "Mecânico Teste",
                CpfNumber = "11144477735",
                Role = new RoleResponse
                {
                    Id = Guid.NewGuid(),
                    Name = "MECHANIC",
                },
            });

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var created = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Sem potência",
            Observations = "Revisar ignição",
        }, Guid.NewGuid(), CancellationToken.None);

        await service.Assign(created.CreatedId, assigneeId, performerId, "Atribuição inicial", CancellationToken.None);

        var workOrder = await context.WorkOrders
            .AsNoTracking()
            .FirstAsync(item => item.Id == created.CreatedId, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(assigneeId, workOrder.AssignedToUserId);
        Assert.AreEqual(WorkOrderStatus.UnderDiagnosis, workOrder.Status);
    }

    [TestMethod("Mudança de status é idempotente para mensagens duplicadas")]
    public async Task It_ShouldBeIdempotent_WhenStatusIsRepeated()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var performedByUserId = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var created = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Alinhamento",
            Observations = "Puxando para direita",
        }, Guid.NewGuid(), CancellationToken.None);

        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.UnderDiagnosis, performedByUserId, null, CancellationToken.None);
        var changed = await service.ChangeStatus(created.CreatedId, WorkOrderStatus.UnderDiagnosis, performedByUserId, null,
            CancellationToken.None);

        var statusChangedHistory = await context.WorkOrderHistories
            .AsNoTracking()
            .Where(item => item.WorkOrderId == created.CreatedId && item.Action == "StatusChanged")
            .ToListAsync(TestContext.CancellationTokenSource.Token);

        Assert.IsFalse(changed);
        Assert.AreEqual(1, statusChangedHistory.Count);
    }

    [TestMethod("Survey de entrega não é reenviado em status duplicado")]
    public async Task It_ShouldNotSendDeliveredSurveyTwice_WhenDeliveredIsDuplicated()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var performedByUserId = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var created = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Troca de bateria",
            Observations = "Bateria antiga descarregando",
        }, Guid.NewGuid(), CancellationToken.None);

        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.UnderDiagnosis, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.PendingApproval, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.InProgress, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.Completed, performedByUserId, null, CancellationToken.None);
        await service.ApplyPaymentApprovedEvent(new PaymentApprovedEvent
        {
            WorkOrderId = created.CreatedId,
            PaidAt = DateTimeOffset.UtcNow,
        }, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.Delivered, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.Delivered, performedByUserId, null, CancellationToken.None);

        _emailServiceMock.Verify(
            service => service.SendWorkOrderDeliveredSurvey(
                It.IsAny<Mechanics.Domain.Customers.Customer>(),
                It.IsAny<WorkOrder>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod("Marca pagamento como aprovado pelo evento")]
    public async Task It_ShouldMarkPaymentAsApproved_WhenEventIsConsumed()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var paidAt = DateTimeOffset.UtcNow;

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var created = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Falha elétrica",
            Observations = "Aguardando aprovação de pagamento",
        }, Guid.NewGuid(), CancellationToken.None);

        var changed = await service.ApplyPaymentApprovedEvent(new PaymentApprovedEvent
        {
            WorkOrderId = created.CreatedId,
            PaidAt = paidAt,
        }, CancellationToken.None);

        var workOrder = await context.WorkOrders
            .AsNoTracking()
            .FirstAsync(item => item.Id == created.CreatedId, TestContext.CancellationTokenSource.Token);
        var history = await context.WorkOrderHistories
            .AsNoTracking()
            .Where(item => item.WorkOrderId == created.CreatedId && item.Action == "PaymentApproved")
            .ToListAsync(TestContext.CancellationTokenSource.Token);
        var response = await service.Get(created.CreatedId, CancellationToken.None);

        Assert.IsTrue(changed);
        Assert.IsNotNull(workOrder.PaidAt);
        Assert.AreEqual(paidAt, workOrder.PaidAt.Value);
        Assert.AreEqual(1, history.Count);
        Assert.IsNotNull(response);
        Assert.AreEqual(WorkOrderStatus.Received, response.Status);
        Assert.IsTrue(response.IsPaymentApproved);
        Assert.IsFalse(response.IsReadyForDelivery);
    }

    [TestMethod("Rejeita evento de pagamento sem data válida")]
    public async Task It_ShouldRejectPaymentEvent_WhenPaidAtIsDefault()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var created = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Ruído no freio",
            Observations = "Teste de validação do evento",
        }, Guid.NewGuid(), CancellationToken.None);

        var exception = await Assert.ThrowsExactlyAsync<BusinessException>(() =>
            service.ApplyPaymentApprovedEvent(new PaymentApprovedEvent
            {
                WorkOrderId = created.CreatedId,
                PaidAt = default,
            }, CancellationToken.None));

        Assert.AreEqual("Invalid payment-approved event: PaidAt is required.", exception.Message);
    }

    [TestMethod("Bloqueia entrega quando pagamento não foi aprovado")]
    public async Task It_ShouldBlockDelivery_WhenPaymentWasNotApproved()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var performedByUserId = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var created = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Troca de óleo",
            Observations = "Sem observações adicionais",
        }, Guid.NewGuid(), CancellationToken.None);

        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.UnderDiagnosis, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.PendingApproval, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.InProgress, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.Completed, performedByUserId, null, CancellationToken.None);

        var exception = await Assert.ThrowsExactlyAsync<BusinessException>(() =>
            service.ChangeStatus(created.CreatedId, WorkOrderStatus.Delivered, performedByUserId, null, CancellationToken.None));

        Assert.AreEqual("Work order must be paid before delivery.", exception.Message);
    }

    [TestMethod("Libera retirada quando OS está finalizada e pagamento aprovado")]
    public async Task It_ShouldSetReadyForDelivery_WhenCompletedAndPaid()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var performedByUserId = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var created = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Revisão completa",
            Observations = "Aguardando pagamento",
        }, Guid.NewGuid(), CancellationToken.None);

        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.UnderDiagnosis, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.PendingApproval, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.InProgress, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.Completed, performedByUserId, null, CancellationToken.None);

        await service.ApplyPaymentApprovedEvent(new PaymentApprovedEvent
        {
            WorkOrderId = created.CreatedId,
            PaidAt = DateTimeOffset.UtcNow,
        }, CancellationToken.None);

        var response = await service.Get(created.CreatedId, CancellationToken.None);
        Assert.IsNotNull(response);
        Assert.AreEqual(WorkOrderStatus.ReadyForDelivery, response.Status);
        Assert.IsTrue(response.IsPaymentApproved);
        Assert.IsTrue(response.IsReadyForDelivery);
    }

    [TestMethod("Muda para pronto para retirada ao finalizar OS já paga")]
    public async Task It_ShouldMoveToReadyForPickup_WhenCompletingAlreadyPaidWorkOrder()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var performedByUserId = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var created = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Falha no alternador",
            Observations = "Pagamento recebido antes da finalização",
        }, Guid.NewGuid(), CancellationToken.None);

        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.UnderDiagnosis, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.PendingApproval, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.InProgress, performedByUserId, null, CancellationToken.None);

        await service.ApplyPaymentApprovedEvent(new PaymentApprovedEvent
        {
            WorkOrderId = created.CreatedId,
            PaidAt = DateTimeOffset.UtcNow,
        }, CancellationToken.None);

        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.Completed, performedByUserId, null, CancellationToken.None);
        var response = await service.Get(created.CreatedId, CancellationToken.None);
        Assert.IsNotNull(response);
        Assert.AreEqual(WorkOrderStatus.ReadyForDelivery, response.Status);
        Assert.IsTrue(response.IsPaymentApproved);
        Assert.IsTrue(response.IsReadyForDelivery);
    }

    [TestMethod("Fluxo completo: criação, execução, pagamento e retirada")]
    public async Task It_ShouldFollowEndToEndFlow_FromCreationToDelivery()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var performedByUserId = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var created = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Perda de potência",
            Observations = "Fluxo ponta a ponta",
        }, Guid.NewGuid(), CancellationToken.None);

        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.UnderDiagnosis, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.PendingApproval, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.InProgress, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.Completed, performedByUserId, null, CancellationToken.None);

        await Assert.ThrowsExactlyAsync<BusinessException>(() =>
            service.ChangeStatus(created.CreatedId, WorkOrderStatus.Delivered, performedByUserId, null, CancellationToken.None));

        await service.ApplyPaymentApprovedEvent(new PaymentApprovedEvent
        {
            WorkOrderId = created.CreatedId,
            PaidAt = DateTimeOffset.UtcNow,
        }, CancellationToken.None);

        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.Delivered, performedByUserId, null, CancellationToken.None);

        var response = await service.Get(created.CreatedId, CancellationToken.None);
        Assert.IsNotNull(response);
        Assert.AreEqual(WorkOrderStatus.Delivered, response.Status);
        Assert.IsTrue(response.IsPaymentApproved);
        Assert.IsFalse(response.IsReadyForDelivery);
    }

    [TestMethod("Fluxo de revisão: retorna para diagnóstico após pendente")]
    public async Task It_ShouldAllowReturningToDiagnosis_FromPendingApproval()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var performedByUserId = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var created = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Barulho metálico",
            Observations = "Cliente recusou primeiro orçamento",
        }, Guid.NewGuid(), CancellationToken.None);

        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.UnderDiagnosis, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.PendingApproval, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.UnderDiagnosis, performedByUserId, "Novo diagnóstico", CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.PendingApproval, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.InProgress, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.Completed, performedByUserId, null, CancellationToken.None);

        var response = await service.Get(created.CreatedId, CancellationToken.None);
        Assert.IsNotNull(response);
        Assert.AreEqual(WorkOrderStatus.Completed, response.Status);
        Assert.IsFalse(response.IsPaymentApproved);
        Assert.IsFalse(response.IsReadyForDelivery);
    }

    private WorkOrderAppService BuildService(Mechanics.Infra.Data.AppDbContext context) =>
        new(
            context,
            _mapper,
            _emailServiceMock.Object,
            _eventPublisherMock.Object,
            _userServiceMock.Object,
            NullLogger<WorkOrderAppService>.Instance);
}
