using System.Globalization;
using InvoiceBuilder.Application.Clients;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceBuilder.Web.Controllers;

/// <summary>Client statement download (spec §4.2) as PDF or CSV, for the selected business.</summary>
[ApiController]
[Authorize(Policy = "Staff")]
[Route("api/clients")]
public sealed class StatementController(ISender sender) : ControllerBase
{
    [HttpGet("{clientId:guid}/statement")]
    public async Task<IActionResult> Statement(Guid clientId, [FromQuery] string from, [FromQuery] string to, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate)
            || !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
        {
            return BadRequest("Use dates like 2026-11-01.");
        }

        var result = await sender.Send(new GetClientStatementFileQuery(clientId, fromDate, toDate, string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code == StatementErrors.ClientNotFound.Code ? NotFound() : BadRequest(result.Error.Message);
        }

        Response.Headers.CacheControl = "no-store";
        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}
