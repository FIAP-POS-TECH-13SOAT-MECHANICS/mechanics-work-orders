using Mechanics.Domain.Base;
using Mechanics.Domain.Base.Exceptions;
using Mechanics.Domain.Base.Validation;
using Mechanics.Domain.Customers;
using Mechanics.Domain.Vehicles;

namespace Mechanics.Tests.Unit.Tests.Domain;

[TestClass]
[TestCategory("Domain")]
public class DomainCoverageBoostTests
{
    [TestMethod("AbstractEntity: igualdade por Id")]
    public void It_ShouldCompareEntitiesById()
    {
        var id = Guid.NewGuid();
        var left = new FakeEntity { Id = id };
        var right = new FakeEntity { Id = id };

        Assert.IsTrue(left.Equals(right));
        Assert.AreEqual(id.GetHashCode(), left.GetHashCode());
    }

    [TestMethod("AbstractEntity: desigualdade com Id diferente e objeto inválido")]
    public void It_ShouldReturnFalse_WhenEntityComparisonIsInvalid()
    {
        var left = new FakeEntity { Id = Guid.NewGuid() };
        var right = new FakeEntity { Id = Guid.NewGuid() };

        Assert.IsFalse(left.Equals(right));
        Assert.IsFalse(left.Equals("not-an-entity"));
    }

    [TestMethod("EntityNotFoundException: ThrowIfNull com entidade existente não lança")]
    public void It_ShouldNotThrow_WhenEntityExists()
    {
        var entity = new FakeEntity { Id = Guid.NewGuid() };
        EntityNotFoundException.ThrowIfNull(entity, entity.Id);
        EntityNotFoundException.ThrowIfNull(entity, "ABC123");
        EntityNotFoundException.ThrowIfNotFound<FakeEntity>(true, entity.Id);
    }

    [TestMethod("EntityNotFoundException: ThrowIfNull por chave deve compor mensagem")]
    public void It_ShouldThrowWithKeyMessage_WhenEntityIsNullByKey()
    {
        var key = Guid.NewGuid();
        var ex = Assert.ThrowsExactly<EntityNotFoundException>(() => EntityNotFoundException.ThrowIfNull<FakeEntity>(null, key));

        Assert.AreEqual($"FakeEntity with key '{key}' not found.", ex.Message);
    }

    [TestMethod("EntityNotFoundException: ThrowIfNull por propriedade deve compor mensagem")]
    public void It_ShouldThrowWithPropertyMessage_WhenEntityIsNullByProperty()
    {
        var document = "12345678900";
        var ex = Assert.ThrowsExactly<EntityNotFoundException>(() => EntityNotFoundException.ThrowIfNull<FakeEntity>(null, document));

        Assert.AreEqual($"FakeEntity with document '{document}' not found.", ex.Message);
    }

    [TestMethod("EntityNotFoundException: ThrowIfNull com propriedade nula usa mensagem padrão")]
    public void It_ShouldThrowDefaultMessage_WhenEntityIsNullAndPropertyValueIsNull()
    {
        object? propertyValue = null;
        var ex = Assert.ThrowsExactly<EntityNotFoundException>(() =>
            EntityNotFoundException.ThrowIfNull<FakeEntity>(null, propertyValue!));

        Assert.AreEqual("FakeEntity not found.", ex.Message);
    }

    [TestMethod("EntityNotFoundException: ThrowIfNotFound false usa mensagem sem chave")]
    public void It_ShouldThrowDefaultMessage_WhenFoundFlagIsFalse()
    {
        var ex = Assert.ThrowsExactly<EntityNotFoundException>(() =>
            EntityNotFoundException.ThrowIfNotFound<FakeEntity>(false, null));

        Assert.AreEqual("FakeEntity not found.", ex.Message);
    }

    [TestMethod("Validator: retorna true e não lança quando válido")]
    public void It_ShouldValidateAndNotThrow_WhenValidatableIsValid()
    {
        var validatable = new FakeValidatable(true);

        Assert.IsTrue(Validator.Validate(validatable));
        Validator.ValidateAndThrow(validatable);
        Validator.BuildAndThrow(builder => builder.AddValidation(true, "Field", "ignored"));
    }

    [TestMethod("Validator: retorna false e lança quando inválido")]
    public void It_ShouldReturnFalseAndThrow_WhenValidatableIsInvalid()
    {
        var invalidatable = new FakeValidatable(false);

        Assert.IsFalse(Validator.Validate(invalidatable));
        Assert.ThrowsExactly<DomainValidationException>(() => Validator.ValidateAndThrow(invalidatable));
        Assert.ThrowsExactly<DomainValidationException>(() =>
            Validator.BuildAndThrow(builder => builder.AddValidation(false, "Field", "error")));
    }

    [TestMethod("LicensePlate: normaliza, formata e converte para string")]
    public void It_ShouldNormalizeFormatAndConvertLicensePlate()
    {
        var plate = new LicensePlate(" abc-1d23 ");
        string asString = plate;

        Assert.AreEqual("ABC1D23", plate.Number);
        Assert.AreEqual("ABC1D23", plate.ToString());
        Assert.AreEqual("ABC1D23", asString);
        Assert.AreEqual("ABC-1D23", plate.Format());
        Assert.IsTrue(plate.IsNormalized());
    }

    [TestMethod("LicensePlate: validação falha para tamanho e formato inválidos")]
    public void It_ShouldInvalidateLicensePlate_WhenLengthOrFormatIsInvalid()
    {
        var invalidLengthPlate = new LicensePlate("ABC123");
        var invalidFormatPlate = new LicensePlate("AB12C34");

        Assert.IsFalse(Validator.Validate(invalidLengthPlate));
        Assert.IsFalse(Validator.Validate(invalidFormatPlate));
    }

    [TestMethod("LicensePlate: normalização manual e igualdade")]
    public void It_ShouldNormalizeManuallyAndCompareLicensePlates()
    {
        var first = new LicensePlate("AAA1B23");
        first.Number = " aa-a1b23 ";
        first.Normalize();

        var same = new LicensePlate("AAA1B23");
        var different = new LicensePlate("BBB2C34");

        Assert.AreEqual("AAA1B23", first.Number);
        Assert.IsTrue(first.Equals(same));
        Assert.IsFalse(first.Equals(different));
        Assert.IsFalse(first.Equals("AAA1B23"));
        Assert.AreEqual(first.Number.GetHashCode(), first.GetHashCode());
    }

    [TestMethod("PersonalDocument: string, igualdade e hashcode")]
    public void It_ShouldConvertCompareAndHashPersonalDocument()
    {
        var first = new PersonalDocument(DocumentType.Cpf, "935.568.660-99");
        first.Normalize();
        string asString = first;

        var same = new PersonalDocument(DocumentType.Cpf, "93556866099");
        var different = new PersonalDocument(DocumentType.Cpf, "11706848072");

        Assert.AreEqual("93556866099", asString);
        Assert.AreEqual("93556866099", first.ToString());
        Assert.IsTrue(first.Equals(same));
        Assert.IsFalse(first.Equals(different));
        Assert.IsFalse(first.Equals("93556866099"));
        Assert.AreEqual(first.Number.GetHashCode(), first.GetHashCode());
    }

    private sealed class FakeEntity : AbstractEntity;

    private sealed class FakeValidatable(bool valid) : IValidatable
    {
        public void Validate(ValidationBuilder builder) =>
            builder.AddValidation(valid, "Field", "Invalid field");
    }
}
