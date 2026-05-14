using FluentValidation;
using Mechanics.Application.WorkOrders.Requests;

namespace Mechanics.Application.WorkOrders.Validators;

public class CreateWorkOrderRequestValidator : AbstractValidator<CreateWorkOrderRequest>
{
    public CreateWorkOrderRequestValidator()
    {
        RuleFor(r => r.VehicleId).NotEmpty().WithMessage("VehicleId is required.");

        RuleFor(r => r.ReportedProblem)
            .MaximumLength(1000)
            .WithMessage("ReportedProblem cannot exceed 1000 characters.");

        RuleFor(r => r.Observations)
            .MaximumLength(2000)
            .WithMessage("Observations cannot exceed 2000 characters.");
    }
}
