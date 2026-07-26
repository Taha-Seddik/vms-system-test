using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vms.Api.Extensions;
using Vms.Api.Models;
using Vms.Api.Services;

namespace Vms.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.OperatorOrAdministrator)]
[Route("api/events")]
public sealed class EventsController(EventService events) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<EventSearchResponse>> Search(
        [FromQuery] EventQuery request,
        CancellationToken cancellationToken)
    {
        if (request.From > request.To)
        {
            ModelState.AddModelError(
                nameof(request.From),
                "From must be earlier than or equal to To.");
            return ValidationProblem(ModelState);
        }

        return Ok(await events.SearchAsync(request, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EventResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await events.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<EventResponse>> Close(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await events.CloseAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
