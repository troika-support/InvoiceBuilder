using FluentValidation;
using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Behaviours;
using InvoiceBuilder.Domain.Common;
using MediatR;
using NSubstitute;

namespace InvoiceBuilder.Application.Tests;

public sealed class BehaviourTests
{
    private sealed record Ping(string Name) : IRequest<Result>;

    private sealed class PingValidator : AbstractValidator<Ping>
    {
        public PingValidator() => RuleFor(p => p.Name).NotEmpty().WithMessage("Enter a name.");
    }

    private sealed record TransactionalPing : ITransactionalCommand<Result>;

    [Fact]
    public async Task Validation_failure_returns_field_errors_without_calling_handler()
    {
        var behaviour = new ValidationBehaviour<Ping, Result>([new PingValidator()]);
        var called = false;

        var result = await behaviour.Handle(new Ping(""), _ => { called = true; return Task.FromResult(Result.Success()); }, CancellationToken.None);

        called.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Failed");
        result.Error.FieldErrors!["Name"].ShouldBe(["Enter a name."]);
    }

    [Fact]
    public async Task Validation_failure_of_generic_result_keeps_the_type()
    {
        var behaviour = new ValidationBehaviour<GenericPing, Result<int>>([new GenericPingValidator()]);

        var result = await behaviour.Handle(new GenericPing(""), _ => Task.FromResult(Result.Success(1)), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.FieldErrors!.ContainsKey("Name").ShouldBeTrue();
    }

    [Fact]
    public async Task Validation_success_calls_handler()
    {
        var behaviour = new ValidationBehaviour<Ping, Result>([new PingValidator()]);

        var result = await behaviour.Handle(new Ping("ok"), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Transactional_command_is_saved_and_committed_on_success()
    {
        var (uow, scope, dispatcher) = Substitutes();
        var behaviour = new UnitOfWorkBehaviour<TransactionalPing, Result>(uow, dispatcher);

        await behaviour.Handle(new TransactionalPing(), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await scope.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transactional_command_failure_is_not_saved()
    {
        var (uow, scope, dispatcher) = Substitutes();
        var behaviour = new UnitOfWorkBehaviour<TransactionalPing, Result>(uow, dispatcher);

        await behaviour.Handle(new TransactionalPing(), _ => Task.FromResult(Result.Failure(new Error("X", "no"))), CancellationToken.None);

        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await scope.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Plain_request_does_not_open_a_transaction()
    {
        var (uow, _, dispatcher) = Substitutes();
        var behaviour = new UnitOfWorkBehaviour<Ping, Result>(uow, dispatcher);

        await behaviour.Handle(new Ping("x"), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        await uow.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    private static (IUnitOfWork Uow, ITransactionScope Scope, IDomainEventDispatcher Dispatcher) Substitutes()
    {
        var uow = Substitute.For<IUnitOfWork>();
        var scope = Substitute.For<ITransactionScope>();
        uow.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(scope);
        uow.TakeDomainEvents().Returns([]);
        return (uow, scope, Substitute.For<IDomainEventDispatcher>());
    }

    private sealed record GenericPing(string Name) : IRequest<Result<int>>;

    private sealed class GenericPingValidator : AbstractValidator<GenericPing>
    {
        public GenericPingValidator() => RuleFor(p => p.Name).NotEmpty();
    }
}
