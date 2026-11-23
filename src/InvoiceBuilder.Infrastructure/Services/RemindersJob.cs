using Hangfire;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Messaging;
using InvoiceBuilder.Domain.Documents;
using InvoiceBuilder.Domain.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InvoiceBuilder.Infrastructure.Services;

/// <summary>
/// Daily reminders (R30, R39). One combined message per invoice per day; the unique ReminderLog row makes a second run
/// the same day send nothing (R31). Runs per business so every query stays tenant and business scoped.
/// </summary>
public sealed partial class RemindersJob(
    IReminderRepository reminders,
    IDocumentRepository documents,
    IClientRepository clients,
    IPaymentRepository payments,
    IScopeOverride scope,
    ISender sender,
    IUnitOfWork unitOfWork,
    IDisplayFormatter format,
    TimeProvider timeProvider,
    ILogger<RemindersJob> logger)
{
    private static readonly TimeSpan Harare = TimeSpan.FromHours(2);

    [Queue("default")]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime((now + Harare).DateTime);
        var businesses = await reminders.ListBusinessesAsync(cancellationToken);
        foreach (var business in businesses)
        {
            try
            {
                scope.UseScope(business.TenantId, business.BusinessId);
                await RunBusinessAsync(business, today, now, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogBusinessFailed(logger, ex, business.BusinessId);
            }
        }
    }

    private async Task RunBusinessAsync(ReminderBusiness business, DateOnly today, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var invoice in await reminders.ListOpenInvoicesAsync(cancellationToken))
        {
            if (invoice.Number is null || await reminders.HasLogAsync(invoice.Id, today, cancellationToken))
            {
                continue;
            }

            var credited = (await documents.ListCreditNotesAsync(invoice.Id, cancellationToken)).Sum(c => c.Total);
            var paid = await payments.ConfirmedAllocatedTotalAsync(invoice.Id, cancellationToken);
            var items = BuildItems(invoice, credited, paid, today);
            var decision = ReminderPlanner.Plan(today, business.Rules, items);
            if (decision is null)
            {
                continue;
            }

            var client = await clients.GetByIdAsync(invoice.ClientId, cancellationToken);
            var email = string.IsNullOrWhiteSpace(client?.Email) ? null : client.Email;
            var whatsApp = email is null ? (string.IsNullOrWhiteSpace(client?.WhatsApp) ? client?.Phone : client.WhatsApp) : null;
            if (email is null && string.IsNullOrWhiteSpace(whatsApp))
            {
                continue;
            }

            var channel = email is not null ? MessageChannel.Email : MessageChannel.WhatsApp;
            reminders.Add(ReminderLog.Create(invoice.TenantId, invoice.BusinessId, invoice.Id, decision.Kind, today, channel, decision.Items.Select(i => i.Position), now));
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var note = Describe(decision, invoice.Currency.ToString());
            var sent = await sender.Send(new SendDocumentCommand(invoice.Id, email, whatsApp, note, null, decision.Items.Any(i => i.Position > 0) ? MessageTemplates.InstalmentReminderEmail : MessageTemplates.ReminderEmail), cancellationToken);
            if (sent.IsFailure)
            {
                LogSendFailed(logger, invoice.Id, sent.Error.Code);
            }
        }
    }

    private static List<ReminderItem> BuildItems(Document invoice, decimal credited, decimal paid, DateOnly today)
    {
        if (invoice.Instalments.Count == 0)
        {
            var remaining = invoice.Total - credited - paid;
            return invoice.DueDate is { } due ? [new ReminderItem(0, 1, due, remaining)] : [];
        }

        var states = invoice.GetInstalmentStates(paid, credited, today);
        return [.. states.Where(s => s.DueDate is not null && s.Status != InstalmentStatus.Paid).Select(s => new ReminderItem(s.Position, states.Count, s.DueDate!.Value, s.Remaining))];
    }

    private string Describe(ReminderDecision decision, string currency) =>
        string.Join("; ", decision.Items.Select(i =>
        {
            var due = format.Date(new DateTimeOffset(i.Due.ToDateTime(TimeOnly.MinValue), Harare));
            var amount = format.Money(i.Remaining, currency);
            return i.Position == 0 ? $"{amount} is due {due}" : $"Instalment {i.Position} of {i.Count}, {amount}, due {due}";
        }));

    [LoggerMessage(Level = LogLevel.Error, Message = "Reminders failed for business {BusinessId}")]
    private static partial void LogBusinessFailed(ILogger logger, Exception exception, Guid businessId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reminder for invoice {InvoiceId} could not be queued: {Code}")]
    private static partial void LogSendFailed(ILogger logger, Guid invoiceId, string code);
}
