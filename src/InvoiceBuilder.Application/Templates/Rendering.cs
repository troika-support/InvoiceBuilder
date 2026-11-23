namespace InvoiceBuilder.Application.Templates;

/// <summary>The one renderer (R25): preview, client view and PDF all call this. A pure function of (definition, model).</summary>
public interface ITemplateRenderer
{
    Task<string> RenderHtmlAsync(TemplateDefinition definition, DocumentRenderModel model, CancellationToken cancellationToken = default);
}

public interface IPdfRenderer
{
    Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken = default);
}
