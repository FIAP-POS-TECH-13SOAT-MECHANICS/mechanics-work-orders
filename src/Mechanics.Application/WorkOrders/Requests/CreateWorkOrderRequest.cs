namespace Mechanics.Application.WorkOrders.Requests;

public class CreateWorkOrderRequest
{
    public required Guid VehicleId { get; init; }
    public string? ReportedProblem { get; init; }
    public string? Observations { get; init; }
}
