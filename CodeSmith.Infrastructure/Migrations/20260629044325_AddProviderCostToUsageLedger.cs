using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeSmith.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderCostToUsageLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ProviderCostUsd",
                table: "UsageLedgerEntries",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderCostUsd",
                table: "UsageLedgerEntries");
        }
    }
}
