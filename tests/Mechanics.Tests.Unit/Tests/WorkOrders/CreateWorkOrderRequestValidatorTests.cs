using Mechanics.Application.WorkOrders.Requests;
using Mechanics.Application.WorkOrders.Validators;

namespace Mechanics.Tests.Unit.Tests.WorkOrders;

[TestClass]
[TestCategory("WorkOrders")]
public class CreateWorkOrderRequestValidatorTests
{
    private readonly CreateWorkOrderRequestValidator _validator = new();

    [TestMethod("Valida request de criação de OS com payload válido")]
    public void It_ShouldValidate_WhenRequestIsValid()
    {
        var request = new CreateWorkOrderRequest
        {
            VehicleId = Guid.NewGuid(),
            ReportedProblem = "Motor apagando em baixa rotação",
            Observations = "Cliente relatou aumento no consumo",
        };

        var result = _validator.Validate(request);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod("Rejeita request sem VehicleId")]
    public void It_ShouldInvalidate_WhenVehicleIdIsEmpty()
    {
        var request = new CreateWorkOrderRequest
        {
            VehicleId = Guid.Empty,
            ReportedProblem = "Falha ao dar partida",
            Observations = "Sem observações",
        };

        var result = _validator.Validate(request);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateWorkOrderRequest.VehicleId)));
    }

    [TestMethod("Rejeita request com ReportedProblem acima de 1000 caracteres")]
    public void It_ShouldInvalidate_WhenReportedProblemExceedsLimit()
    {
        var request = new CreateWorkOrderRequest
        {
            VehicleId = Guid.NewGuid(),
            ReportedProblem = new string('A', 1001),
            Observations = "Observações válidas",
        };

        var result = _validator.Validate(request);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateWorkOrderRequest.ReportedProblem)));
    }

    [TestMethod("Rejeita request com Observations acima de 2000 caracteres")]
    public void It_ShouldInvalidate_WhenObservationsExceedsLimit()
    {
        var request = new CreateWorkOrderRequest
        {
            VehicleId = Guid.NewGuid(),
            ReportedProblem = "Problema válido",
            Observations = new string('B', 2001),
        };

        var result = _validator.Validate(request);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateWorkOrderRequest.Observations)));
    }
}
