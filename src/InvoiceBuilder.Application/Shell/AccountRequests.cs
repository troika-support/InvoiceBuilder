using FluentValidation;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Common;
using MediatR;

namespace InvoiceBuilder.Application.Shell;

/// <summary>The signed-in user's own name, shown in greetings and the activity log.</summary>
public sealed record GetMyAccountQuery : IRequest<Result<MyAccount>>;

public sealed record MyAccount(string DisplayName);

public sealed class GetMyAccountHandler(ICurrentUser user, IUserPreferenceStore users) : IRequestHandler<GetMyAccountQuery, Result<MyAccount>>
{
    public async Task<Result<MyAccount>> Handle(GetMyAccountQuery request, CancellationToken cancellationToken)
    {
        if (user.UserId is not { } userId)
        {
            return Result.Failure<MyAccount>(ShellErrors.NotSignedIn);
        }

        return Result.Success(new MyAccount(await users.GetDisplayNameAsync(userId, cancellationToken) ?? string.Empty));
    }
}

public sealed record SetMyDisplayNameCommand(string DisplayName) : IRequest<Result>;

public sealed class SetMyDisplayNameValidator : AbstractValidator<SetMyDisplayNameCommand>
{
    public SetMyDisplayNameValidator()
    {
        RuleFor(c => c.DisplayName).NotEmpty().WithMessage("Enter your name.").MaximumLength(100);
    }
}

public sealed class SetMyDisplayNameHandler(ICurrentUser user, IUserPreferenceStore users) : IRequestHandler<SetMyDisplayNameCommand, Result>
{
    public async Task<Result> Handle(SetMyDisplayNameCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (user.UserId is not { } userId)
        {
            return Result.Failure(ShellErrors.NotSignedIn);
        }

        await users.SetDisplayNameAsync(userId, request.DisplayName.Trim(), cancellationToken);
        return Result.Success();
    }
}
