using Mechanics.Domain.Base;
using Mechanics.Domain.Customers;
using Mechanics.Domain.Vehicles;

namespace Mechanics.Domain.WorkOrders;

/// <summary>
///     Representa uma ordem de serviço vinculada a um cliente e veículo.
/// </summary>
public class WorkOrder : AbstractEntity
{
    public required Guid CustomerId { get; init; }
    public Customer? Customer { get; init; }

    /// <summary>
    ///     Chave de acesso para consulta pelo cliente.
    /// </summary>
    public required string AccessKey { get; init; }

    public required Guid VehicleId { get; init; }
    public Vehicle? Vehicle { get; init; }

    public WorkOrderStatus Status { get; set; }
    public DateTime LastUpdate { get; set; }

    /// <summary>
    ///     Problema relatado pelo cliente.
    /// </summary>
    public string? ReportedProblem { get; init; }

    /// <summary>
    ///     Observações internas da oficina.
    /// </summary>
    public string? Observations { get; set; }

    /// <summary>
    ///     Data da entrega/retirada do veículo.
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>
    ///     Usuário que realizou a última alteração de status.
    /// </summary>
    public Guid? LastStatusChangedByUserId { get; set; }

    /// <summary>
    ///     Usuário a quem a OS foi atribuída (mecânico).
    /// </summary>
    public Guid? AssignedToUserId { get; set; }

    /// <summary>
    ///     Usuário que criou a OS (referência externa).
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    ///     Gera uma nova chave de acesso única por cliente.
    /// </summary>
    /// <param name="existingOrders">As ordens de serviço do cliente.</param>
    /// <remarks>A chave é composta por 8 dígitos e deve ser única por cliente.</remarks>
    /// <returns>Uma nova chave de acesso para ser usada em <see cref="AccessKey"/>.</returns>
    public static string GenerateNewAccessKey(IEnumerable<WorkOrder> existingOrders)
    {
        var existingKeys = existingOrders.Select(order => order.AccessKey).ToHashSet();

        while (true)
        {
            var newKey = string.Concat(Enumerable.Range(0, 8).Select(_ => Random.Shared.Next(0, 10)));
            if (existingKeys.Add(newKey))
                return newKey;
        }
    }

}
