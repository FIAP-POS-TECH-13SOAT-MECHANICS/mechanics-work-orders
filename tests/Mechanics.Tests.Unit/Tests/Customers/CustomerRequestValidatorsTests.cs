using Mechanics.Application.Customers.Requests;
using Mechanics.Application.Customers.Validators;
using Mechanics.Domain.Customers;
using Mechanics.Tests.Unit.Mocks;

namespace Mechanics.Tests.Unit.Tests.Customers;

[TestClass]
[TestCategory("Customers")]
public class CustomerRequestValidatorsTests
{
    [TestMethod("Valida criação de cliente PF com payload válido")]
    public void It_ShouldValidateIndividualCustomer_WhenRequestIsValid()
    {
        var validator = new CreateIndividualCustomerRequestValidator();
        var request = CustomerMocks.BuildCreateRequestPf();

        var result = validator.Validate(request);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod("Rejeita criação de cliente PF com e-mail inválido")]
    public void It_ShouldInvalidateIndividualCustomer_WhenEmailIsInvalid()
    {
        var validator = new CreateIndividualCustomerRequestValidator();
        var request = new CreateIndividualCustomerRequest
        {
            FullName = "Joao da Silva",
            Email = "email-invalido",
            CpfNumber = "11144477735",
        };

        var result = validator.Validate(request);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateIndividualCustomerRequest.Email)));
    }

    [TestMethod("Rejeita criação de cliente PF com campos obrigatórios vazios")]
    public void It_ShouldInvalidateIndividualCustomer_WhenRequiredFieldsAreEmpty()
    {
        var validator = new CreateIndividualCustomerRequestValidator();
        var request = new CreateIndividualCustomerRequest
        {
            FullName = string.Empty,
            Email = string.Empty,
            CpfNumber = string.Empty,
        };

        var result = validator.Validate(request);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateIndividualCustomerRequest.FullName)));
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateIndividualCustomerRequest.Email)));
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateIndividualCustomerRequest.CpfNumber)));
    }

    [TestMethod("Valida criação de cliente PJ com payload válido")]
    public void It_ShouldValidateBusinessCustomer_WhenRequestIsValid()
    {
        var validator = new CreateBusinessCustomerRequestValidator();
        var request = CustomerMocks.BuildCreateRequestPj();

        var result = validator.Validate(request);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod("Rejeita criação de cliente PJ com e-mail do responsável inválido")]
    public void It_ShouldInvalidateBusinessCustomer_WhenResponsibleEmailIsInvalid()
    {
        var validator = new CreateBusinessCustomerRequestValidator();
        var request = new CreateBusinessCustomerRequest
        {
            CompanyName = "Empresa XYZ",
            CnpjNumber = "11444777000161",
            ResponsibleFullName = "Responsavel",
            ResponsibleEmail = "email-invalido",
            ResponsibleCpfNumber = "11144477735",
        };

        var result = validator.Validate(request);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateBusinessCustomerRequest.ResponsibleEmail)));
    }

    [TestMethod("Rejeita criação de cliente PJ com campos obrigatórios vazios")]
    public void It_ShouldInvalidateBusinessCustomer_WhenRequiredFieldsAreEmpty()
    {
        var validator = new CreateBusinessCustomerRequestValidator();
        var request = new CreateBusinessCustomerRequest
        {
            CompanyName = string.Empty,
            CnpjNumber = string.Empty,
            ResponsibleFullName = string.Empty,
            ResponsibleEmail = string.Empty,
            ResponsibleCpfNumber = string.Empty,
        };

        var result = validator.Validate(request);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateBusinessCustomerRequest.CompanyName)));
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateBusinessCustomerRequest.CnpjNumber)));
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateBusinessCustomerRequest.ResponsibleFullName)));
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateBusinessCustomerRequest.ResponsibleEmail)));
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == nameof(CreateBusinessCustomerRequest.ResponsibleCpfNumber)));
    }

    [TestMethod("Valida atualização quando documento não é informado")]
    public void It_ShouldValidateUpdate_WhenDocumentIsMissing()
    {
        var validator = new UpdateCustomerRequestValidator();
        var request = new UpdateCustomerRequest { Name = "Novo Nome", Email = "novo@teste.com" };

        var result = validator.Validate(request);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod("Rejeita atualização quando número do documento está vazio")]
    public void It_ShouldInvalidateUpdate_WhenDocumentNumberIsEmpty()
    {
        var validator = new UpdateCustomerRequestValidator();
        var request = new UpdateCustomerRequest
        {
            Name = "Novo Nome",
            Email = "novo@teste.com",
            Document = new PersonalDocumentRequest
            {
                Type = DocumentType.Cpf,
                Number = string.Empty,
            },
        };

        var result = validator.Validate(request);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == "Document.Number"));
    }

    [TestMethod("Rejeita atualização quando tipo do documento está fora do enum")]
    public void It_ShouldInvalidateUpdate_WhenDocumentTypeIsOutOfRange()
    {
        var validator = new UpdateCustomerRequestValidator();
        var request = new UpdateCustomerRequest
        {
            Name = "Novo Nome",
            Email = "novo@teste.com",
            Document = new PersonalDocumentRequest
            {
                Type = (DocumentType)999,
                Number = "52998224725",
            },
        };

        var result = validator.Validate(request);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(item => item.PropertyName == "Document.Type"));
    }

    [TestMethod("Valida atualização com documento completo")]
    public void It_ShouldValidateUpdate_WhenDocumentIsValid()
    {
        var validator = new UpdateCustomerRequestValidator();
        var request = new UpdateCustomerRequest
        {
            Name = "Novo Nome",
            Email = "novo@teste.com",
            Document = new PersonalDocumentRequest
            {
                Type = DocumentType.Cpf,
                Number = "52998224725",
            },
        };

        var result = validator.Validate(request);

        Assert.IsTrue(result.IsValid);
    }
}
