using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Messaging;
using MediatR;

namespace InvoiceBuilder.Application.Messaging;

public sealed record SendFailureRow(MessageChannel Channel, string To, string Error, DateTimeOffset At);

public sealed record GetSendFailuresQuery(Guid DocumentId) : IRequest<Result<IReadOnlyList<SendFailureRow>>>;

public sealed class GetSendFailuresHandler(IOutboundMessageRepository messages)
    : IRequestHandler<GetSendFailuresQuery, Result<IReadOnlyList<SendFailureRow>>>
{
    public async Task<Result<IReadOnlyList<SendFailureRow>>> Handle(GetSendFailuresQuery request, CancellationToken cancellationToken)
    {
        var all = await messages.ListForDocumentAsync(request.DocumentId, cancellationToken);
        return Result.Success<IReadOnlyList<SendFailureRow>>(
            all.GroupBy(m => m.Channel)
                .Select(g => g.OrderByDescending(m => m.CreatedAt).First())
                .Where(m => m.Status == MessageStatus.Failed)
                .Select(m => new SendFailureRow(m.Channel, m.To, m.Error ?? "Could not be sent.", m.CreatedAt))
                .ToList());
    }
}
