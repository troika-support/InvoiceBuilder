using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceBuilder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Clients");
        }
    }
}
