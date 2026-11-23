using InvoiceBuilder.Application.Templates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceBuilder.Web.Controllers;

/// <summary>Test PDF from the designer: the saved draft rendered with sample data through the one renderer (R25).</summary>
[ApiController]
[Authorize(Policy = "Owner")]
[Route("api/templates")]
public sealed class TemplateTestPdfController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}/test-pdf")]
    public async Task<IActionResult> TestPdf(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TemplateTestPdfQuery(id), cancellationToken);
        return result.IsFailure ? NotFound() : File(result.Value, "application/pdf", "template-test.pdf");
    }
}
