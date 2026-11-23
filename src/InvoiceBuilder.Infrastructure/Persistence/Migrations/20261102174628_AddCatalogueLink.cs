using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceBuilder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogueLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CatalogueItemId",
                table: "DocumentLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLines_CatalogueItemId",
                table: "DocumentLines",
                column: "CatalogueItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentLines_CatalogueItems_CatalogueItemId",
                table: "DocumentLines",
                column: "CatalogueItemId",
                principalTable: "CatalogueItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentLines_CatalogueItems_CatalogueItemId",
                table: "DocumentLines");

            migrationBuilder.DropIndex(
                name: "IX_DocumentLines_CatalogueItemId",
                table: "DocumentLines");

            migrationBuilder.DropColumn(
                name: "CatalogueItemId",
                table: "DocumentLines");
        }
    }
}
