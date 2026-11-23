using System.Reflection;
using FluentValidation;
using InvoiceBuilder.Domain.Common;
using MediatR;

namespace InvoiceBuilder.Application.Behaviours;

/// <summary>Runs FluentValidation before the handler. Failures come back as a <see cref="Result"/> with field errors; the handler is not called.</summary>
public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private static readonly MethodInfo GenericFailure = typeof(Result)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Result.Failure) && m.IsGenericMethodDefinition);

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var validatorList = validators.ToList();
        if (validatorList.Count == 0)
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);
        var results = new List<FluentValidation.Results.ValidationResult>(validatorList.Count);
        foreach (var validator in validatorList)
        {
            results.Add(await validator.ValidateAsync(context, cancellationToken));
        }

        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();
        if (failures.Count == 0)
        {
            return await next(cancellationToken);
        }

        var fieldErrors = failures
            .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var error = new Error("Validation.Failed", "Some fields need fixing.", fieldErrors);

        return (TResponse)CreateFailure(error);
    }

    private static Result CreateFailure(Error error)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return Result.Failure(error);
        }

        var valueType = typeof(TResponse).GetGenericArguments()[0];
        return (Result)GenericFailure.MakeGenericMethod(valueType).Invoke(null, [error])!;
    }
}
