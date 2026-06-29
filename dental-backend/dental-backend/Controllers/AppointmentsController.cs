using dental_backend.Dentally;
using dental_backend.Models;
using dental_backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace dental_backend.Controllers;

[ApiController]
[Route("api")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly ISyncService _sync;
    private readonly ILogger<AppointmentsController> _logger;

    public AppointmentsController(ISyncService sync, ILogger<AppointmentsController> logger)
    {
        _sync = sync;
        _logger = logger;
    }

    /// <summary>Triggers a sync from Dentally for the requested range and overwrites the local cache.</summary>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(SyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Sync([FromBody] SyncRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _sync.SyncAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Invalid request");
        }
        catch (DentallyApiException ex)
        {
            _logger.LogWarning(ex, "Dentally sync failed.");
            var status = ex.StatusCode switch
            {
                401 or 403 => StatusCodes.Status401Unauthorized,
                429 => StatusCodes.Status429TooManyRequests,
                _ => StatusCodes.Status502BadGateway
            };
            return Problem(detail: ex.Message, statusCode: status, title: "Dentally sync failed");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client aborted the request (cancel button) — no body is meaningful here.
            return StatusCode(499);
        }
    }

    /// <summary>Returns the locally cached appointments from the last successful sync.</summary>
    [HttpGet("appointments")]
    [ProducesResponseType(typeof(SyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetAppointments(CancellationToken ct)
    {
        var cached = await _sync.GetCachedAsync(ct);
        return cached is null ? NoContent() : Ok(cached);
    }

    /// <summary>Returns the full detail for a single appointment (cache enriched with live data).</summary>
    [HttpGet("appointments/{id}")]
    [ProducesResponseType(typeof(AppointmentDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppointment(string id, CancellationToken ct)
    {
        var detail = await _sync.GetAppointmentDetailAsync(id, ct);
        return detail is null ? NotFound() : Ok(detail);
    }
}
