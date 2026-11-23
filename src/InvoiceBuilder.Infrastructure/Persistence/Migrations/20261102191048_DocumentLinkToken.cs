using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceBuilder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentLinkToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AllowedPaymentMethods",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<string>(
                name: "LinkTokenHash",
                table: "Documents",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_LinkTokenHash",
                table: "Documents",
                column: "LinkTokenHash",
                unique: true,
                filter: "[LinkTokenHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_LinkTokenHash",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "AllowedPaymentMethods",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "LinkTokenHash",
                table: "Documents");
        }
    }
}
