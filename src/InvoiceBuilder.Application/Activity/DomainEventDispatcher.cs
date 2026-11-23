using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Payments;

namespace InvoiceBuilder.Application.Activity;

/// <summary>Turns the domain events that matter for the audit trail (R33) into activity entries.</summary>
public sealed class DomainEventDispatcher(IActivityLog activityLog, IDisplayFormatter formatter, InvoiceBuilder.Application.Dashboard.IDashboardCache? dashboard = null) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var domainEvent in events)
        {
            // Issued documents, payments and acceptances change the dashboard figures (prompt 22).
            switch (domainEvent)
            {
                case DocumentIssued e:
                    dashboard?.Invalidate(e.BusinessId);
                    break;
                case QuoteAccepted e:
                    dashboard?.Invalidate(e.BusinessId);
                    break;
                case PaymentConfirmed e:
                    dashboard?.Invalidate(e.BusinessId);
                    break;
            }

            switch (domainEvent)
            {
                case DocumentIssued issued:
                    await activityLog.AddAsync("Document", issued.DocumentId, "Issued", $"Issued {issued.Number}", cancellationToken);
                    break;
                case DocumentViewed viewed:
                    await activityLog.AddAsync("Document", viewed.DocumentId, "Viewed", "Client opened the link", cancellationToken);
                    break;
                case QuoteAccepted accepted:
                    await activityLog.AddAsync("Document", accepted.DocumentId, "Accepted", $"Accepted by {accepted.SignerName}", cancellationToken);
                    break;
                case PaymentConfirmed confirmed:
                    await activityLog.AddAsync(
                        "Payment",
                        confirmed.PaymentId,
                        "PaymentConfirmed",
                        $"Confirmed {formatter.Money(confirmed.Amount, confirmed.Currency.Code)}",
                        cancellationToken);
                    break;
            }
        }
    }
}
