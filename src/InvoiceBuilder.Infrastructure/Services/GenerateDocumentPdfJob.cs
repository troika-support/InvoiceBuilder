using Hangfire;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Documents;
using InvoiceBuilder.Application.Templates;

namespace InvoiceBuilder.Infrastructure.Services;

/// <summary>Enqueues the stored PDF render on the "pdf" queue (spec §9, template-rendering.md §5).</summary>
public sealed class HangfirePdfJobScheduler(IBackgroundJobClient jobs, ICurrentTenant tenant, ICurrentBusiness business) : IPdfJobScheduler
{
    public void Enqueue(Guid documentId)
    {
        if (business.BusinessId is not { } businessId)
        {
            throw new InvalidOperationException("A business must be selected to queue a PDF.");
        }

        var tenantId = tenant.TenantId;
        jobs.Enqueue<GenerateDocumentPdfJob>(j => j.RunAsync(documentId, tenantId, businessId, CancellationToken.None));
    }
}

public sealed class GenerateDocumentPdfJob(
    IScopeOverride scope,
    IDocumentRepository documents,
    IDocumentHtmlService html,
    IPdfRenderer pdf,
    IFileStore files,
    InvoiceBuilder.Application.Documents.ClientLinkService links)
{
    [Queue("pdf")]
    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync(Guid documentId, Guid tenantId, Guid businessId, CancellationToken cancellationToken)
    {
        scope.UseScope(tenantId, businessId);
        var document = await documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null || document.Number is null || document.TemplateVersionId is not { } versionId)
        {
            return;
        }

        var key = PdfKeys.For(document.Id, versionId);
        await using (var existing = await files.OpenAsync(key, cancellationToken))
        {
            if (existing is not null)
            {
                return;
            }
        }

        // The stored PDF carries the document's client link so its "pay online" QR code works.
        var rendered = await html.RenderAsync(document, versionId, links.UrlFor(document), cancellationToken);
        if (rendered.IsFailure)
        {
            throw new InvalidOperationException($"Could not render {document.Number.Value}: {rendered.Error.Message}");
        }

        var bytes = await pdf.RenderAsync(rendered.Value, cancellationToken);
        using var stream = new MemoryStream(bytes);
        await files.SaveAsync(key, stream, cancellationToken);
    }
}
