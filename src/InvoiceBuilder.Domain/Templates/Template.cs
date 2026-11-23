using InvoiceBuilder.Domain.Common;
using InvoiceBuilder.Domain.Documents;

namespace InvoiceBuilder.Domain.Templates;

/// <summary>A published version is immutable (R5, R25); changes publish a new version.</summary>
public sealed class TemplateVersion : BusinessEntity
{
    private TemplateVersion()
    {
    }

    public Guid TemplateId { get; private set; }

    public int Version { get; private set; }

    public string Definition { get; private set; } = "{}";

    public DateTimeOffset? PublishedAt { get; private set; }

    public bool IsDraft => PublishedAt is null;

    internal static TemplateVersion CreateDraft(Template template, int version, string definition, DateTimeOffset now)
    {
        var v = new TemplateVersion
        {
            TenantId = template.TenantId,
            BusinessId = template.BusinessId,
            TemplateId = template.Id,
            Version = version,
            Definition = definition,
        };
        v.Initialise(now);
        return v;
    }

    internal void ReplaceDefinition(string definition, DateTimeOffset now)
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("A published template version is immutable (R5, R25).");
        }

        Definition = definition;
        Touch(now);
    }

    public Guid? PublishedBy { get; private set; }

    internal void MarkPublished(Guid publishedBy, DateTimeOffset now)
    {
        PublishedAt = now;
        PublishedBy = publishedBy;
        Touch(now);
    }

    internal static TemplateVersion CreatePublished(Template template, int version, string definition, Guid publishedBy, DateTimeOffset now)
    {
        var v = new TemplateVersion
        {
            TenantId = template.TenantId,
            BusinessId = template.BusinessId,
            TemplateId = template.Id,
            Version = version,
            Definition = definition,
            PublishedAt = now,
            PublishedBy = publishedBy,
        };
        v.Initialise(now);
        return v;
    }
}

public sealed class Template : BusinessEntity
{
    private readonly List<TemplateVersion> _versions = [];

    private Template()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public IReadOnlySet<DocumentType> DocumentTypes { get; private set; } = new HashSet<DocumentType>();

    public Guid ThemeId { get; private set; }

    public Guid? CurrentVersionId { get; private set; }

    public bool IsArchived { get; private set; }

    public IReadOnlyList<TemplateVersion> Versions => _versions;

    /// <summary>Archived templates are hidden from lists and pickers; issued documents keep their version (R22).</summary>
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

    public static Result<Template> Create(
        Guid tenantId,
        Guid businessId,
        string name,
        IEnumerable<DocumentType> documentTypes,
        Guid themeId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Template>(new Error("Template.NameRequired", "A template needs a name."));
        }

        var template = new Template
        {
            TenantId = tenantId,
            BusinessId = businessId,
            Name = name.Trim(),
            DocumentTypes = documentTypes.ToHashSet(),
            ThemeId = themeId,
        };
        template.Initialise(now);
        return Result.Success(template);
    }

    public TemplateVersion? DraftVersion => _versions.FirstOrDefault(v => v.IsDraft);

    /// <summary>Saves the working draft, creating it on first save. Published versions are never touched.</summary>
    public TemplateVersion SaveDraft(string definition, DateTimeOffset now)
    {
        var draft = DraftVersion;
        if (draft is not null)
        {
            draft.ReplaceDefinition(definition, now);
        }
        else
        {
            draft = TemplateVersion.CreateDraft(this, (_versions.Count == 0 ? 0 : _versions.Max(v => v.Version)) + 1, definition, now);
            _versions.Add(draft);
        }

        Touch(now);
        return draft;
    }

    /// <summary>Turns the working draft into the next immutable version. Earlier versions are kept (R21, R22).</summary>
    public Result<TemplateVersion> PublishDraft(Guid publishedBy, DateTimeOffset now)
    {
        var draft = DraftVersion;
        if (draft is null)
        {
            return Result.Failure<TemplateVersion>(new Error("Template.NoDraft", "There is no draft to publish."));
        }

        draft.MarkPublished(publishedBy, now);
        CurrentVersionId = draft.Id;
        Touch(now);
        return Result.Success(draft);
    }

    public TemplateVersion PublishVersion(string definition, Guid publishedBy, DateTimeOffset now)
    {
        var next = (_versions.Count == 0 ? 0 : _versions.Max(v => v.Version)) + 1;
        var version = TemplateVersion.CreatePublished(this, next, definition, publishedBy, now);
        _versions.Add(version);
        CurrentVersionId = version.Id;
        Touch(now);
        return version;
    }
}
