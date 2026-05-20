using Mechanics.Application.Vehicles.Requests;
using Mechanics.Application.Vehicles.Validators;
using Mechanics.Tests.Unit.Helpers;
using Mechanics.Tests.Unit.Mocks;

namespace Mechanics.Tests.Unit.Tests.Vehicles;

[TestClass]
[TestCategory("Vehicles")]
public class UpdateVehicleRequestValidatorTests
{
    [TestMethod("Valida atualização sem campos opcionais")]
    public async Task It_ShouldValidate_WhenOptionalFieldsAreMissing()
    {
        await using var context = new DbContextTestBuilder().Build();
        var validator = new UpdateVehicleRequestValidator(context);
        var request = new UpdateVehicleRequest { Id = Guid.NewGuid() };

        var result = await validator.ValidateAsync(request);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod("Rejeita atualização quando ownerId informado não existe")]
    public async Task It_ShouldInvalidate_WhenOwnerDoesNotExist()
    {
        await using var context = new DbContextTestBuilder().Build();
        var validator = new UpdateVehicleRequestValidator(context);
        var request = new UpdateVehicleRequest
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
        };

        var result = await validator.ValidateAsync(request);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName.Contains(nameof(UpdateVehicleRequest.OwnerId))));
    }

    [TestMethod("Valida atualização quando ownerId informado existe")]
    public async Task It_ShouldValidate_WhenOwnerExists()
    {
        var ownerId = Guid.NewGuid();
        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(ownerId)])
            .Build();
        var validator = new UpdateVehicleRequestValidator(context);
        var request = new UpdateVehicleRequest
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
        };

        var result = await validator.ValidateAsync(request);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod("Rejeita atualização quando placa conflita com outro veículo")]
    public async Task It_ShouldInvalidate_WhenLicensePlateBelongsToAnotherVehicle()
    {
        var ownerId = Guid.NewGuid();
        var currentVehicleId = Guid.NewGuid();
        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(ownerId)])
            .WithData(
            [
                VehicleMocks.CreateVehicle(currentVehicleId, ownerId, plate: "ABC1D23", chassis: "9BWZZZ377VT000001"),
                VehicleMocks.CreateVehicle(Guid.NewGuid(), ownerId, plate: "XYZ1A23", chassis: "9BWZZZ377VT000002"),
            ])
            .Build();
        var validator = new UpdateVehicleRequestValidator(context);
        var request = new UpdateVehicleRequest
        {
            Id = currentVehicleId,
            LicensePlate = "XYZ1A23",
        };

        var result = await validator.ValidateAsync(request);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(UpdateVehicleRequest.LicensePlate)));
    }

    [TestMethod("Permite manter a mesma placa no próprio veículo")]
    public async Task It_ShouldValidate_WhenLicensePlateBelongsToCurrentVehicle()
    {
        var ownerId = Guid.NewGuid();
        var currentVehicleId = Guid.NewGuid();
        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(ownerId)])
            .WithData([VehicleMocks.CreateVehicle(currentVehicleId, ownerId, plate: "ABC1D23", chassis: "9BWZZZ377VT000001")])
            .Build();
        var validator = new UpdateVehicleRequestValidator(context);
        var request = new UpdateVehicleRequest
        {
            Id = currentVehicleId,
            LicensePlate = "ABC1D23",
        };

        var result = await validator.ValidateAsync(request);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod("Rejeita atualização quando chassi conflita com outro veículo")]
    public async Task It_ShouldInvalidate_WhenChassisBelongsToAnotherVehicle()
    {
        var ownerId = Guid.NewGuid();
        var currentVehicleId = Guid.NewGuid();
        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(ownerId)])
            .WithData(
            [
                VehicleMocks.CreateVehicle(currentVehicleId, ownerId, plate: "ABC1D23", chassis: "9BWZZZ377VT000001"),
                VehicleMocks.CreateVehicle(Guid.NewGuid(), ownerId, plate: "XYZ1A23", chassis: "9BWZZZ377VT000002"),
            ])
            .Build();
        var validator = new UpdateVehicleRequestValidator(context);
        var request = new UpdateVehicleRequest
        {
            Id = currentVehicleId,
            Chassis = "9BWZZZ377VT000002",
        };

        var result = await validator.ValidateAsync(request);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(UpdateVehicleRequest.Chassis)));
    }

    [TestMethod("Permite manter o mesmo chassi no próprio veículo")]
    public async Task It_ShouldValidate_WhenChassisBelongsToCurrentVehicle()
    {
        var ownerId = Guid.NewGuid();
        var currentVehicleId = Guid.NewGuid();
        await using var context = new DbContextTestBuilder()
            .WithData([CustomerMocks.CreateCustomerPf(ownerId)])
            .WithData([VehicleMocks.CreateVehicle(currentVehicleId, ownerId, plate: "ABC1D23", chassis: "9BWZZZ377VT000001")])
            .Build();
        var validator = new UpdateVehicleRequestValidator(context);
        var request = new UpdateVehicleRequest
        {
            Id = currentVehicleId,
            Chassis = "9BWZZZ377VT000001",
        };

        var result = await validator.ValidateAsync(request);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod("Rejeita atualização com placa vazia quando o campo é enviado")]
    public async Task It_ShouldInvalidate_WhenLicensePlateIsEmpty()
    {
        await using var context = new DbContextTestBuilder().Build();
        var validator = new UpdateVehicleRequestValidator(context);
        var request = new UpdateVehicleRequest
        {
            Id = Guid.NewGuid(),
            LicensePlate = string.Empty,
        };

        var result = await validator.ValidateAsync(request);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(UpdateVehicleRequest.LicensePlate)));
    }

    [TestMethod("Rejeita atualização com chassi vazio quando o campo é enviado")]
    public async Task It_ShouldInvalidate_WhenChassisIsEmpty()
    {
        await using var context = new DbContextTestBuilder().Build();
        var validator = new UpdateVehicleRequestValidator(context);
        var request = new UpdateVehicleRequest
        {
            Id = Guid.NewGuid(),
            Chassis = string.Empty,
        };

        var result = await validator.ValidateAsync(request);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(UpdateVehicleRequest.Chassis)));
    }
}
