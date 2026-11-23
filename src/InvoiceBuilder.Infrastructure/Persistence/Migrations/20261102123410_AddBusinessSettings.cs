using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceBuilder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankDetails = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EmailFromName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmailReplyTo = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    WhatsAppPhoneNumberId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WhatsAppTokenEncrypted = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RemindersEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ReminderDaysBeforeDue = table.Column<int>(type: "int", nullable: false),
                    ReminderOnDueDate = table.Column<bool>(type: "bit", nullable: false),
                    ReminderRepeatEveryDays = table.Column<int>(type: "int", nullable: false),
                    ReminderMaxAfterDue = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessSettings_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessSettings_BusinessId",
                table: "BusinessSettings",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessSettings_TenantId",
                table: "BusinessSettings",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessSettings");
        }
    }
}
