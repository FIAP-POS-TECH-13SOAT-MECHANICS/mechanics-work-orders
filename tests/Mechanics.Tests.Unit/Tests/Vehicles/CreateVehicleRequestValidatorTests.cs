using Mechanics.Application.Vehicles.Requests;
using Mechanics.Application.Vehicles.Validators;
using Mechanics.Domain.Vehicles;
using Mechanics.Tests.Unit.Helpers;
using Mechanics.Tests.Unit.Mocks;

namespace Mechanics.Tests.Unit.Tests.Vehicles;

[TestClass]
[TestCategory("Vehicles")]
public class CreateVehicleRequestValidatorTests
{
    [TestMethod("Valida cadastro de veículo quando dados e relacionamentos são válidos")]
    public async Task It_ShouldValidate_WhenRequestIsValid()
    {
        var ownerId = Guid.NewGuid();
        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(ownerId)])
            .Build();
        var validator = new CreateVehicleRequestValidator(context);
        var request = VehicleMocks.BuildCreateRequest(ownerId);

        var result = await validator.ValidateAsync(request);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod("Rejeita cadastro quando placa já existe")]
    public async Task It_ShouldInvalidate_WhenLicensePlateAlreadyExists()
    {
        var ownerId = Guid.NewGuid();
        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(ownerId)])
            .WithData([VehicleMocks.CreateVehicle(Guid.NewGuid(), ownerId, plate: "ABC1D23", chassis: "9BWZZZ377VT000001")])
            .Build();
        var validator = new CreateVehicleRequestValidator(context);
        var request = new CreateVehicleRequest
        {
            Manufacturer = "Ford",
            Model = "Ka",
            Color = VehicleColor.Black,
            Year = "2022",
            LicensePlate = "ABC1D23",
            Chassis = "9BWZZZ377VT000002",
            OwnerId = ownerId,
        };

        var result = await validator.ValidateAsync(request);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateVehicleRequest.LicensePlate)));
    }

    [TestMethod("Rejeita cadastro quando chassi já existe")]
    public async Task It_ShouldInvalidate_WhenChassisAlreadyExists()
    {
        var ownerId = Guid.NewGuid();
        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(ownerId)])
            .WithData([VehicleMocks.CreateVehicle(Guid.NewGuid(), ownerId, plate: "ABC1D23", chassis: "9BWZZZ377VT000001")])
            .Build();
        var validator = new CreateVehicleRequestValidator(context);
        var request = new CreateVehicleRequest
        {
            Manufacturer = "Ford",
            Model = "Ka",
            Color = VehicleColor.Black,
            Year = "2022",
            LicensePlate = "XYZ1A23",
            Chassis = "9BWZZZ377VT000001",
            OwnerId = ownerId,
        };

        var result = await validator.ValidateAsync(request);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateVehicleRequest.Chassis)));
    }

    [TestMethod("Rejeita cadastro quando ownerId não existe")]
    public async Task It_ShouldInvalidate_WhenOwnerDoesNotExist()
    {
        await using var context = new DbContextTestBuilder().Build();
        var validator = new CreateVehicleRequestValidator(context);
        var request = VehicleMocks.BuildCreateRequest(Guid.NewGuid());

        var result = await validator.ValidateAsync(request);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateVehicleRequest.OwnerId)));
    }

    [TestMethod("Rejeita cadastro com dados obrigatórios vazios")]
    public async Task It_ShouldInvalidate_WhenRequiredFieldsAreEmpty()
    {
        var ownerId = Guid.NewGuid();
        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(ownerId)])
            .Build();
        var validator = new CreateVehicleRequestValidator(context);
        var request = new CreateVehicleRequest
        {
            Manufacturer = string.Empty,
            Model = string.Empty,
            Color = VehicleColor.Black,
            Year = string.Empty,
            LicensePlate = string.Empty,
            Chassis = string.Empty,
            OwnerId = ownerId,
        };

        var result = await validator.ValidateAsync(request);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateVehicleRequest.Manufacturer)));
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateVehicleRequest.Model)));
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateVehicleRequest.Year)));
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateVehicleRequest.LicensePlate)));
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateVehicleRequest.Chassis)));
    }
}
