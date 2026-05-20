using AutoMapper;
using Mechanics.Application.Identity.Responses;
using Mechanics.Application.Identity.Services;
using Mechanics.Application.Notification.Services;
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

    [TestMethod("Falha ao atribuir quando usuário não existe")]
    public async Task It_ShouldThrow_WhenAssignedUserDoesNotExist()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();

        _userServiceMock
            .Setup(service => service.GetUserById(assigneeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserResponse?)null);

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var created = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Motor falhando",
            Observations = "Sem potência na subida",
        }, Guid.NewGuid(), CancellationToken.None);

        await Assert.ThrowsExactlyAsync<BusinessException>(async () =>
            await service.Assign(created.CreatedId, assigneeId, Guid.NewGuid(), null, CancellationToken.None));
    }

    [TestMethod("Falha ao atribuir quando usuário não é mecânico")]
    public async Task It_ShouldThrow_WhenAssignedUserIsNotMechanic()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();

        _userServiceMock
            .Setup(service => service.GetUserById(assigneeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse
            {
                Id = assigneeId,
                FullName = "Atendente Teste",
                CpfNumber = "11144477735",
                Role = new RoleResponse
                {
                    Id = Guid.NewGuid(),
                    Name = "ATTENDANT",
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
            ReportedProblem = "Falha no freio",
            Observations = "Pedal baixo",
        }, Guid.NewGuid(), CancellationToken.None);

        await Assert.ThrowsExactlyAsync<BusinessException>(async () =>
            await service.Assign(created.CreatedId, assigneeId, Guid.NewGuid(), null, CancellationToken.None));
    }

    [TestMethod("Atribuição não força auto-transição quando OS já saiu de Received")]
    public async Task It_ShouldNotAutoChangeStatus_WhenWorkOrderIsNotReceived()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();

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
            ReportedProblem = "Ruído em curva",
            Observations = "Avaliar suspensão",
        }, Guid.NewGuid(), CancellationToken.None);

        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.UnderDiagnosis, performedBy, null, CancellationToken.None);
        await service.Assign(created.CreatedId, assigneeId, performedBy, null, CancellationToken.None);

        var statusChanges = await context.WorkOrderHistories
            .AsNoTracking()
            .Where(item => item.WorkOrderId == created.CreatedId && item.Action == "StatusChanged")
            .ToListAsync(TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(1, statusChanges.Count);
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
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.Delivered, performedByUserId, null, CancellationToken.None);
        await service.ChangeStatus(created.CreatedId, WorkOrderStatus.Delivered, performedByUserId, null, CancellationToken.None);

        _emailServiceMock.Verify(
            service => service.SendWorkOrderDeliveredSurvey(
                It.IsAny<Mechanics.Domain.Customers.Customer>(),
                It.IsAny<WorkOrder>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod("Falha ao alterar status com transição inválida")]
    public async Task It_ShouldThrow_WhenStatusTransitionIsInvalid()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var created = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Sem refrigeração",
            Observations = "Ar não gela",
        }, Guid.NewGuid(), CancellationToken.None);

        await Assert.ThrowsExactlyAsync<BusinessException>(async () =>
            await service.ChangeStatus(created.CreatedId, WorkOrderStatus.Completed, performedBy, null, CancellationToken.None));
    }

    [TestMethod("Consulta por access key ignora espaços")]
    public async Task It_ShouldTrackWorkOrderByAccessKey_IgnoringSpaces()
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
            ReportedProblem = "Pneu furado",
            Observations = "Troca necessária",
        }, Guid.NewGuid(), CancellationToken.None);

        var workOrder = await context.WorkOrders
            .AsNoTracking()
            .FirstAsync(item => item.Id == created.CreatedId, TestContext.CancellationTokenSource.Token);

        var accessKeyWithSpaces = $"{workOrder.AccessKey[..4]} {workOrder.AccessKey[4..]}";
        var tracked = await service.TrackByAccessKey(customerId, accessKeyWithSpaces, CancellationToken.None);

        Assert.IsNotNull(tracked);
        Assert.AreEqual(created.CreatedId, tracked.Id);
    }

    [TestMethod("Consulta por access key retorna null quando não encontra")]
    public async Task It_ShouldReturnNull_WhenTrackByAccessKeyDoesNotMatch()
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
            ReportedProblem = "Direção desalinhada",
            Observations = "Puxa para esquerda",
        }, Guid.NewGuid(), CancellationToken.None);

        var tracked = await service.TrackByAccessKey(customerId, "99999999", CancellationToken.None);
        Assert.IsNull(tracked);
    }

    [TestMethod("Consulta por access key falha quando customer não existe")]
    public async Task It_ShouldThrow_WhenTrackingWorkOrderForNonExistentCustomer()
    {
        await using var context = new DbContextTestBuilder().Build();
        var service = BuildService(context);

        await Assert.ThrowsExactlyAsync<EntityNotFoundException>(async () =>
            await service.TrackByAccessKey(Guid.NewGuid(), "12345678", CancellationToken.None));
    }

    [TestMethod("Não atualiza detalhes quando observação é nula")]
    public async Task It_ShouldNotUpdateDetails_WhenObservationsIsNull()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var created = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Pintura desgastada",
            Observations = "Obs inicial",
        }, Guid.NewGuid(), CancellationToken.None);

        await service.UpdateDetails(created.CreatedId, new UpdateWorkOrderRequest
        {
            Observations = null,
        }, performedBy, CancellationToken.None);

        var updates = await context.WorkOrderHistories
            .AsNoTracking()
            .Where(item => item.WorkOrderId == created.CreatedId && item.Action == "DetailsUpdated")
            .ToListAsync(TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(0, updates.Count);
    }

    [TestMethod("Não atualiza detalhes quando observação não muda")]
    public async Task It_ShouldNotUpdateDetails_WhenObservationsDoesNotChange()
    {
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var performedBy = Guid.NewGuid();

        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(customerId)])
            .WithData([VehicleMocks.CreateVehicle(vehicleId, customerId)])
            .Build();

        var service = BuildService(context);
        var created = await service.Create(new CreateWorkOrderRequest
        {
            VehicleId = vehicleId,
            ReportedProblem = "Ajuste de embreagem",
            Observations = "Manter regulagem",
        }, Guid.NewGuid(), CancellationToken.None);

        await service.UpdateDetails(created.CreatedId, new UpdateWorkOrderRequest
        {
            Observations = "Manter regulagem",
        }, performedBy, CancellationToken.None);

        var updates = await context.WorkOrderHistories
            .AsNoTracking()
            .Where(item => item.WorkOrderId == created.CreatedId && item.Action == "DetailsUpdated")
            .ToListAsync(TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(0, updates.Count);
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
