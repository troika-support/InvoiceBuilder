using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Domain.Clients;
using InvoiceBuilder.Domain.Common;
using MediatR;

namespace InvoiceBuilder.Application.Clients;

public sealed record ClientImportPreview(IReadOnlyList<ClientImportRow> Rows)
{
    public int ValidCount => Rows.Count(r => r.IsValid);

    public int ErrorCount => Rows.Count(r => !r.IsValid);
}

/// <summary>Parses and checks a CSV. Saves nothing.</summary>
public sealed record PreviewClientImportQuery(string Content) : IRequest<Result<ClientImportPreview>>;

public sealed class PreviewClientImportHandler(ICurrentBusiness currentBusiness, IClientQueries queries)
    : IRequestHandler<PreviewClientImportQuery, Result<ClientImportPreview>>
{
    public async Task<Result<ClientImportPreview>> Handle(PreviewClientImportQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is null)
        {
            return Result.Failure<ClientImportPreview>(ClientErrors.NoBusiness);
        }

        try
        {
            var names = await queries.ListNamesAsync(cancellationToken);
            return Result.Success(new ClientImportPreview(ClientCsv.Parse(request.Content, names)));
        }
        catch (FormatException ex)
        {
            return Result.Failure<ClientImportPreview>(new Error("Client.ImportFile", ex.Message));
        }
    }
}

/// <summary>Imports the valid rows of a previewed CSV. The file is checked again so nothing invalid is saved.</summary>
public sealed record ImportClientsCommand(string Content) : ITransactionalCommand<Result<int>>;

public sealed class ImportClientsHandler(
    ICurrentTenant tenant,
    ICurrentBusiness currentBusiness,
    IBusinessRepository businesses,
    IClientRepository clients,
    IClientQueries queries,
    IActivityLog activity,
    TimeProvider timeProvider) : IRequestHandler<ImportClientsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(ImportClientsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentBusiness.BusinessId is not { } businessId
            || await businesses.GetByIdAsync(businessId, cancellationToken) is not { } business)
        {
            return Result.Failure<int>(ClientErrors.NoBusiness);
        }

        IReadOnlyList<ClientImportRow> rows;
        try
        {
            rows = ClientCsv.Parse(request.Content, await queries.ListNamesAsync(cancellationToken));
        }
        catch (FormatException ex)
        {
            return Result.Failure<int>(new Error("Client.ImportFile", ex.Message));
        }

        var now = timeProvider.GetUtcNow();
        var imported = 0;
        foreach (var row in rows.Where(r => r.IsValid))
        {
            var created = Client.Create(tenant.TenantId, businessId, row.Name, business.DefaultCurrency, now);
            if (created.IsFailure)
            {
                continue;
            }

            var client = created.Value;
            client.UpdateContact(row.Email, row.Phone, null, row.Address, null, now);
            clients.Add(client);
            await activity.AddAsync(nameof(Client), client.Id, "ClientImported", $"Client {client.Name} imported from CSV.", cancellationToken);
            imported++;
        }

        return Result.Success(imported);
    }
}
