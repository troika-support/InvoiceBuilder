using InvoiceBuilder.Application.Clients;
using Shouldly;

namespace InvoiceBuilder.Application.Tests.Clients;

public sealed class ClientCsvTests
{
    [Fact]
    public void Parses_header_in_any_order_and_quoted_fields()
    {
        var csv = "Email,Name,Address,Phone\r\n\"a@x.co\",\"Moyo, T\",\"1 Main St\nHarare\",0771\r\n";

        var rows = ClientCsv.Parse(csv, []);

        rows.Count.ShouldBe(1);
        rows[0].Name.ShouldBe("Moyo, T");
        rows[0].Email.ShouldBe("a@x.co");
        rows[0].Phone.ShouldBe("0771");
        rows[0].Address.ShouldBe("1 Main St\nHarare");
        rows[0].Errors.ShouldBeEmpty();
        rows[0].LineNumber.ShouldBe(2);
    }

    [Fact]
    public void Flags_duplicate_in_file_and_against_existing_clients_case_insensitively()
    {
        var csv = "name,email\nAcme,\nacme ,\nBeta,\n";

        var rows = ClientCsv.Parse(csv, ["BETA"]);

        rows[0].Errors.ShouldBeEmpty();
        rows[1].Errors.ShouldContain("Duplicate name in this file.");
        rows[2].Errors.ShouldContain("A client with this name already exists.");
    }

    [Fact]
    public void Flags_bad_email_and_missing_name()
    {
        var csv = "name,email\n,a@x.co\nAcme,not-an-email\n";

        var rows = ClientCsv.Parse(csv, []);

        rows[0].Errors.ShouldContain("Name is missing.");
        rows[1].Errors.ShouldContain("Email address is not valid.");
    }

    [Fact]
    public void Missing_name_column_is_a_file_error()
    {
        Should.Throw<FormatException>(() => ClientCsv.Parse("email,phone\na@x.co,1\n", []))
            .Message.ShouldBe("The file needs a \"name\" column.");
    }

    [Fact]
    public void Blank_lines_are_skipped()
    {
        ClientCsv.Parse("name\n\nAcme\n\n", []).Count.ShouldBe(1);
    }
}
