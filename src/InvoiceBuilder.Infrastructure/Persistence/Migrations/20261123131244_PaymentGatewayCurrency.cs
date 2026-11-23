using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceBuilder.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Paynow issues a separate integration per currency. Existing integrations become the USD one; ZiG is set up
    /// separately in Settings, Payment methods.
    /// </summary>
    public partial class PaymentGatewayCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentGatewayConfigs_BusinessId_Provider",
                table: "PaymentGatewayConfigs");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "PaymentGatewayConfigs",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentGatewayConfigs_BusinessId_Provider_Currency",
                table: "PaymentGatewayConfigs",
                columns: new[] { "BusinessId", "Provider", "Currency" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old unique index allows one integration per business, so the extra currencies have to go first.
            migrationBuilder.Sql("DELETE FROM [PaymentGatewayConfigs] WHERE [Currency] <> N'USD';");

            migrationBuilder.DropIndex(
                name: "IX_PaymentGatewayConfigs_BusinessId_Provider_Currency",
                table: "PaymentGatewayConfigs");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "PaymentGatewayConfigs");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentGatewayConfigs_BusinessId_Provider",
                table: "PaymentGatewayConfigs",
                columns: new[] { "BusinessId", "Provider" },
                unique: true);
        }
    }
}
