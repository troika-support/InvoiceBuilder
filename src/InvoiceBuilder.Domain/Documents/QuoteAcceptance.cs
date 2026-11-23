using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Documents;

public sealed class QuoteAcceptance : BusinessEntity
{
    private QuoteAcceptance()
    {
    }

    public Guid DocumentId { get; private set; }

    public QuoteDecision Decision { get; private set; }

    public string SignerName { get; private set; } = string.Empty;

    public DateTimeOffset AcceptedAt { get; private set; }

    public string? Ip { get; private set; }

    public string? UserAgent { get; private set; }

    public string? Comment { get; private set; }

    internal static QuoteAcceptance Create(
        Document document,
        QuoteDecision decision,
        string signerName,
        string? ip,
        string? userAgent,
        string? comment,
        DateTimeOffset now)
    {
        var acceptance = new QuoteAcceptance
        {
            TenantId = document.TenantId,
            BusinessId = document.BusinessId,
            DocumentId = document.Id,
            Decision = decision,
            SignerName = signerName.Trim(),
            AcceptedAt = now,
            Ip = ip,
            UserAgent = userAgent,
            Comment = comment,
        };
        acceptance.Initialise(now);
        return acceptance;
    }
}
