namespace InvoiceBuilder.Domain.Common;

/// <summary>Base fields for every entity (spec §6): Id (Guid v7), CreatedAt, UpdatedAt, RowVersion.</summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; protected set; }

    public DateTimeOffset UpdatedAt { get; protected set; }

    public byte[] RowVersion { get; protected set; } = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    protected void Initialise(DateTimeOffset now)
    {
        CreatedAt = now;
        UpdatedAt = now;
    }

    protected void Touch(DateTimeOffset now) => UpdatedAt = now;
}

/// <summary>Entity that belongs to a tenant (R1).</summary>
public abstract class TenantEntity : Entity
{
    public Guid TenantId { get; protected set; }
}

/// <summary>Entity that belongs to one business inside a tenant (R1).</summary>
public abstract class BusinessEntity : TenantEntity
{
    public Guid BusinessId { get; protected set; }
}
