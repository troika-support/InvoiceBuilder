using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Notifications;
using MediatR;

namespace InvoiceBuilder.Application.Notifications;

public interface INotificationRepository
{
    void Add(Notification notification);

    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Undismissed items for the current business, newest first.</summary>
    Task<IReadOnlyList<Notification>> ListActiveAsync(CancellationToken cancellationToken = default);
}

public sealed record NotificationRow(Guid Id, NotificationKind Kind, string Title, string Message, string? LinkPath, DateTimeOffset CreatedAt);

public sealed record ListNeedsAttentionQuery : IRequest<IReadOnlyList<NotificationRow>>;

public sealed class ListNeedsAttentionHandler(INotificationRepository notifications)
    : IRequestHandler<ListNeedsAttentionQuery, IReadOnlyList<NotificationRow>>
{
    public async Task<IReadOnlyList<NotificationRow>> Handle(ListNeedsAttentionQuery request, CancellationToken cancellationToken) =>
        (await notifications.ListActiveAsync(cancellationToken))
            .Select(n => new NotificationRow(n.Id, n.Kind, n.Title, n.Message, n.LinkPath, n.CreatedAt))
            .ToList();
}

public sealed record DismissNotificationCommand(Guid NotificationId) : ITransactionalCommand<Result>;

public sealed class DismissNotificationHandler(INotificationRepository notifications, ICurrentBusiness currentBusiness, TimeProvider timeProvider)
    : IRequestHandler<DismissNotificationCommand, Result>
{
    public async Task<Result> Handle(DismissNotificationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var notification = await notifications.GetByIdAsync(request.NotificationId, cancellationToken);
        if (notification is null || notification.BusinessId != currentBusiness.BusinessId)
        {
            return Result.Failure(new Error("Notification.NotFound", "That item no longer exists."));
        }

        notification.Dismiss(timeProvider.GetUtcNow());
        return Result.Success();
    }
}
