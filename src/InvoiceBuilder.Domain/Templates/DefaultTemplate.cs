using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;

namespace InvoiceBuilder.Domain.Templates;

/// <summary>The business's chosen default template for one document type.</summary>
public sealed class DefaultTemplate : BusinessEntity
{
    private DefaultTemplate()
    {
    }

    public DocumentType DocumentType { get; private set; }

    public Guid TemplateId { get; private set; }

    public static DefaultTemplate Create(Guid tenantId, Guid businessId, DocumentType type, Guid templateId, DateTimeOffset now)
    {
        var d = new DefaultTemplate
        {
            TenantId = tenantId,
            BusinessId = businessId,
            DocumentType = type,
            TemplateId = templateId,
        };
        d.Initialise(now);
        return d;
    }

    public void Change(Guid templateId, DateTimeOffset now)
    {
        TemplateId = templateId;
        Touch(now);
    }
}
