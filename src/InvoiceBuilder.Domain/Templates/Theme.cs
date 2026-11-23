using InvoiceBuilder.Domain.Common;

namespace InvoiceBuilder.Domain.Templates;

public sealed class Theme : BusinessEntity
{
    private Theme()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>JSON of colours and fonts.</summary>
    public string Tokens { get; private set; } = "{}";

    public bool IsArchived { get; private set; }

    public void Archive(DateTimeOffset now)
    {
        IsArchived = true;
        Touch(now);
    }

    public void Restore(DateTimeOffset now)
    {
        IsArchived = false;
        Touch(now);
    }

    public Result Update(string name, string tokens, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(new Error("Theme.NameRequired", "A theme needs a name."));
        }

        Name = name.Trim();
        Tokens = tokens;
        Touch(now);
        return Result.Success();
    }

    public static Result<Theme> Create(Guid tenantId, Guid businessId, string name, string tokens, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Theme>(new Error("Theme.NameRequired", "A theme needs a name."));
        }

        var theme = new Theme { TenantId = tenantId, BusinessId = businessId, Name = name.Trim(), Tokens = tokens };
        theme.Initialise(now);
        return Result.Success(theme);
    }
}
