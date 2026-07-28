using Microsoft.AspNetCore.Mvc;
using MyFantasy.Api.Contracts;
using MyFantasy.Api.Fantasy;

namespace MyFantasy.Api.Controllers;

/// <summary>Equipos de LaLiga con su escudo (de teams-master), para el filtro
/// visual de la pestaña General.</summary>
[ApiController]
[Route("api/teams")]
public class TeamsController : ControllerBase
{
    private readonly IFantasyApiClient _api;

    public TeamsController(IFantasyApiClient api) => _api = api;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TeamResponse>>> Get(CancellationToken ct)
    {
        var teams = await _api.GetTeamsMasterAsync(ct);
        var rows = teams
            .Where(t => !string.IsNullOrWhiteSpace(t.Id))
            .Select(t => new TeamResponse(t.Id!, t.Name ?? t.Id!, t.ResolvedBadgeUrl))
            .OrderBy(t => t.Name)
            .ToList();
        return Ok(rows);
    }
}
