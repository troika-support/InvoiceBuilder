using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceBuilder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMorePaymentMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "AllowedPaymentMethods",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 127,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 15);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "AllowedPaymentMethods",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 15,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 127);
        }
    }
}
