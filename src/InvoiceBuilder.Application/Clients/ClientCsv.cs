using System.Net.Mail;
using System.Text;

namespace InvoiceBuilder.Application.Clients;

public sealed record ClientImportRow(int LineNumber, string Name, string? Email, string? Phone, string? Address, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>Parses a client CSV (name, email, phone, address) and flags problems for the import preview.</summary>
public static class ClientCsv
{
    public const int MaxRows = 2000;

    public static IReadOnlyList<ClientImportRow> Parse(string content, IEnumerable<string> existingNames)
    {
        ArgumentNullException.ThrowIfNull(content);
        var records = ReadRecords(content.TrimStart('\uFEFF'));
        if (records.Count == 0)
        {
            throw new FormatException("The file is empty.");
        }

        var header = records[0].Fields.Select(h => h.Trim().ToLowerInvariant()).ToList();
        var nameIndex = header.IndexOf("name");
        if (nameIndex < 0)
        {
            throw new FormatException("The file needs a \"name\" column.");
        }

        if (records.Count - 1 > MaxRows)
        {
            throw new FormatException($"The file has more than {MaxRows} rows. Split it into smaller files.");
        }

        var emailIndex = header.IndexOf("email");
        var phoneIndex = header.IndexOf("phone");
        var addressIndex = header.IndexOf("address");

        var existing = new HashSet<string>(existingNames.Select(n => n.Trim()), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<ClientImportRow>();

        foreach (var record in records.Skip(1))
        {
            if (record.Fields.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var name = Field(record.Fields, nameIndex) ?? string.Empty;
            var email = Field(record.Fields, emailIndex);
            var errors = new List<string>();

            if (name.Length == 0)
            {
                errors.Add("Name is missing.");
            }
            else if (name.Length > 200)
            {
                errors.Add("Name is longer than 200 characters.");
            }
            else if (existing.Contains(name))
            {
                errors.Add("A client with this name already exists.");
            }
            else if (!seen.Add(name))
            {
                errors.Add("Duplicate name in this file.");
            }

            if (email is not null && !IsValidEmail(email))
            {
                errors.Add("Email address is not valid.");
            }

            rows.Add(new ClientImportRow(record.LineNumber, name, email, Field(record.Fields, phoneIndex), Field(record.Fields, addressIndex), errors));
        }

        return rows;
    }

    private static string? Field(List<string> fields, int index)
    {
        if (index < 0 || index >= fields.Count)
        {
            return null;
        }

        var value = fields[index].Trim();
        return value.Length == 0 ? null : value;
    }

    private static bool IsValidEmail(string value) =>
        value.Length <= 254 && MailAddress.TryCreate(value, out var address) && address.Address == value && value.Contains('.', StringComparison.Ordinal);

    private static List<(int LineNumber, List<string> Fields)> ReadRecords(string content)
    {
        var records = new List<(int, List<string>)>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordStart = 1;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < content.Length && content[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    if (c == '\n')
                    {
                        line++;
                    }

                    if (c != '\r')
                    {
                        field.Append(c);
                    }
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add((recordStart, fields));
                    fields = [];
                    line++;
                    recordStart = line;
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add((recordStart, fields));
        }

        return records;
    }
}
