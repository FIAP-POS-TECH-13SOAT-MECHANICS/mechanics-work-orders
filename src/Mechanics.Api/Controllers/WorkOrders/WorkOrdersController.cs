using Mechanics.Application.WorkOrders.Requests;
using Mechanics.Application.WorkOrders.Responses;
using Mechanics.Application.WorkOrders.Services;
using Mechanics.Domain.Auth;
using Mechanics.Infra.Security;
using Mechanics.Infra.Security.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Mechanics.Api.Controllers.WorkOrders;

/// <summary>
///     Controller para gerenciamento de ordens de serviço.
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
[Route("[controller]")]
[Authorize]
public class WorkOrdersController(WorkOrderAppService workOrderService, ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>
    ///     Cria uma nova ordem de serviço.
    /// </summary>
    /// <param name="request">Dados da ordem de serviço.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <response code="201">Ordem criada com sucesso.</response>
    /// <response code="400">Requisição inválida.</response>
    [HttpPost]
    [Authorize(Roles = $"{RoleNames.Administrator},{RoleNames.Attendant},{RoleNames.Mechanic}")]
    [Consumes(typeof(CreateWorkOrderRequest), "application/json")]
    [Produces("application/json", Type = typeof(object))]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetData().UserId;
        var response = await workOrderService.Create(request, userId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = response.CreatedId }, response);
    }

    /// <summary>
    ///     Obtém os detalhes de uma ordem de serviço por id.
    /// </summary>
    /// <param name="id">Identificador da ordem.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <response code="200">Registro encontrado.</response>
    /// <response code="404">Registro não encontrado.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{RoleNames.Administrator},{RoleNames.Attendant},{RoleNames.Mechanic},{RoleNames.Service}")]
    [Produces("application/json", Type = typeof(GetWorkOrderResponse))]
    [ProducesResponseType(typeof(GetWorkOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var response = await workOrderService.Get(id, cancellationToken);
        if (response is null) return NotFound();
        return Ok(response);
    }

    /// <summary>
    ///     Atribui a WorkOrder a um mecânico. (Atendente/Administrator)
    ///     Essa ação grava a atribuição e, se a OS estiver em Received, move automaticamente para UnderDiagnosis.
    /// </summary>
    /// <param name="id">Identificador da ordem.</param>
    /// <param name="request">AssignedToUserId e comentário opcional.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <response code="204">Atribuição realizada.</response>
    /// <response code="400">Requisição inválida.</response>
    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = $"{RoleNames.Attendant},{RoleNames.Administrator}")]
    [Consumes(typeof(AssignWorkOrderRequest), "application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetData().UserId;

        await workOrderService.Assign(id, request.AssignedToUserId, userId, request.Description, cancellationToken);
        return NoContent();
    }

    /// <summary>
    ///     Atualiza campos locais da ordem de serviço.
    /// </summary>
    /// <param name="id">Identificador da ordem.</param>
    /// <param name="request">Dados de atualização.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <response code="204">Ordem atualizada com sucesso.</response>
    /// <response code="400">Requisição inválida.</response>
    /// <response code="401">Usuário não autenticado.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{RoleNames.Administrator},{RoleNames.Attendant},{RoleNames.Mechanic}")]
    [Consumes(typeof(UpdateWorkOrderRequest), "application/json")]
    [Produces("application/json", Type = typeof(object))]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, UpdateWorkOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetData().UserId;

        await workOrderService.UpdateDetails(id, request, userId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    ///     Lista ordens de serviço com filtros opcionais e paginação.
    /// </summary>
    /// <param name="request">Parâmetros de filtro e paginação.</param>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    /// <response code="200">Consulta executada.</response>
    /// <response code="400">Parâmetros inválidos.</response>
    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Administrator},{RoleNames.Attendant},{RoleNames.Mechanic}")]
    [Produces("application/json", Type = typeof(GetWorkOrdersResponse))]
    [ProducesResponseType(typeof(GetWorkOrdersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetWorkOrders([FromQuery] GetWorkOrdersRequest request, CancellationToken cancellationToken)
    {
        var response = await workOrderService.GetList(request, cancellationToken);
        return Ok(response);
    }
}
