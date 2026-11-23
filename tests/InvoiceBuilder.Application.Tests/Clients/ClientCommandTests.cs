using InvoiceBuilder.Application.Abstractions;
using InvoiceBuilder.Application.Clients;
using InvoiceBuilder.Domain.Businesses;
using InvoiceBuilder.Domain.Clients;
using InvoiceBuilder.Domain.ValueObjects;
using NSubstitute;
using Shouldly;

namespace InvoiceBuilder.Application.Tests.Clients;

public sealed class ClientCommandTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BusinessId = Guid.NewGuid();

    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly ICurrentBusiness _business = Substitute.For<ICurrentBusiness>();
    private readonly IBusinessRepository _businesses = Substitute.For<IBusinessRepository>();
    private readonly IClientRepository _clients = Substitute.For<IClientRepository>();
    private readonly IClientQueries _queries = Substitute.For<IClientQueries>();
    private readonly IActivityLog _activity = Substitute.For<IActivityLog>();

    public ClientCommandTests()
    {
        _tenant.TenantId.Returns(TenantId);
        _business.BusinessId.Returns(BusinessId);
        _queries.ListNamesAsync(Arg.Any<CancellationToken>()).Returns(["Existing Co"]);
    }

    private static ClientInput Input(string name = "Acme", string? email = "a@acme.co") =>
        new(name, "USD", email, "0771", null, "1 Main St", null);

    [Fact]
    public async Task R33_create_adds_client_with_contacts_and_is_audited()
    {
        Client? added = null;
        _clients.Add(Arg.Do<Client>(c => added = c));

        var result = await new CreateClientHandler(_tenant, _business, _clients, _queries, _activity, TimeProvider.System)
            .Handle(new CreateClientCommand(Input()), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        added.ShouldNotBeNull();
        added.BusinessId.ShouldBe(BusinessId);
        added.Email.ShouldBe("a@acme.co");
        await _activity.Received(1).AddAsync("Client", added.Id, "ClientCreated", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_rejects_duplicate_name_and_saves_nothing()
    {
        _queries.NameExistsAsync("Acme", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await new CreateClientHandler(_tenant, _business, _clients, _queries, _activity, TimeProvider.System)
            .Handle(new CreateClientCommand(Input()), CancellationToken.None);

        result.Error.ShouldBe(ClientErrors.DuplicateName);
        _clients.DidNotReceive().Add(Arg.Any<Client>());
        await _activity.DidNotReceive().AddAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_rejects_blank_name_and_bad_email()
    {
        var result = new CreateClientValidator().Validate(new CreateClientCommand(Input(name: " ", email: "nope")));

        result.Errors.Select(e => e.PropertyName).ShouldBe(["Input.Name", "Input.Email"], ignoreOrder: true);
    }

    [Fact]
    public async Task R33_update_changes_details_and_is_audited()
    {
        var client = Client.Create(TenantId, BusinessId, "Old", CurrencyCode.Usd, DateTimeOffset.UtcNow).Value;
        _clients.GetByIdAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);

        var result = await new UpdateClientHandler(_clients, _queries, _activity, TimeProvider.System)
            .Handle(new UpdateClientCommand(client.Id, Input("New name")), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        client.Name.ShouldBe("New name");
        await _activity.Received(1).AddAsync("Client", client.Id, "ClientUpdated", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_may_keep_its_own_name_but_not_take_another()
    {
        var client = Client.Create(TenantId, BusinessId, "Acme", CurrencyCode.Usd, DateTimeOffset.UtcNow).Value;
        _clients.GetByIdAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        _queries.NameExistsAsync("Beta", client.Id, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new UpdateClientHandler(_clients, _queries, _activity, TimeProvider.System);

        (await handler.Handle(new UpdateClientCommand(client.Id, Input("Acme")), CancellationToken.None)).IsSuccess.ShouldBeTrue();
        (await handler.Handle(new UpdateClientCommand(client.Id, Input("Beta")), CancellationToken.None)).Error.ShouldBe(ClientErrors.DuplicateName);
    }

    [Fact]
    public async Task R33_archive_hides_client_and_is_audited()
    {
        var client = Client.Create(TenantId, BusinessId, "Acme", CurrencyCode.Usd, DateTimeOffset.UtcNow).Value;
        _clients.GetByIdAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);

        var result = await new ArchiveClientHandler(_clients, _activity, TimeProvider.System)
            .Handle(new ArchiveClientCommand(client.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        client.IsArchived.ShouldBeTrue();
        await _activity.Received(1).AddAsync("Client", client.Id, "ClientArchived", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Archive_unknown_client_is_not_found()
    {
        var result = await new ArchiveClientHandler(_clients, _activity, TimeProvider.System)
            .Handle(new ArchiveClientCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error.ShouldBe(ClientErrors.NotFound);
    }

    [Fact]
    public async Task Preview_saves_nothing()
    {
        var result = await new PreviewClientImportHandler(_business, _queries)
            .Handle(new PreviewClientImportQuery("name,email\nAcme,a@acme.co\n"), CancellationToken.None);

        result.Value.ValidCount.ShouldBe(1);
        _clients.DidNotReceive().Add(Arg.Any<Client>());
    }

    [Fact]
    public async Task Import_adds_only_valid_rows_with_business_currency_and_audits_each()
    {
        var business = Business.Create(TenantId, "Troika", "", "a@b.co", "", DateTimeOffset.UtcNow);
        _businesses.GetByIdAsync(BusinessId, Arg.Any<CancellationToken>()).Returns(business);
        var added = new List<Client>();
        _clients.Add(Arg.Do<Client>(added.Add));
        var csv = "name,email\nAcme,a@acme.co\nAcme,\nexisting co,\nBad,not-an-email\nBeta,\n";

        var result = await new ImportClientsHandler(_tenant, _business, _businesses, _clients, _queries, _activity, TimeProvider.System)
            .Handle(new ImportClientsCommand(csv), CancellationToken.None);

        result.Value.ShouldBe(2);
        added.Select(c => c.Name).ShouldBe(["Acme", "Beta"]);
        added.ShouldAllBe(c => c.DefaultCurrency == business.DefaultCurrency);
        await _activity.Received(2).AddAsync("Client", Arg.Any<Guid>(), "ClientImported", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Import_of_a_bad_file_fails_and_saves_nothing()
    {
        _businesses.GetByIdAsync(BusinessId, Arg.Any<CancellationToken>())
            .Returns(Business.Create(TenantId, "Troika", "", "a@b.co", "", DateTimeOffset.UtcNow));

        var result = await new ImportClientsHandler(_tenant, _business, _businesses, _clients, _queries, _activity, TimeProvider.System)
            .Handle(new ImportClientsCommand("email\na@b.co\n"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        _clients.DidNotReceive().Add(Arg.Any<Client>());
    }
}
