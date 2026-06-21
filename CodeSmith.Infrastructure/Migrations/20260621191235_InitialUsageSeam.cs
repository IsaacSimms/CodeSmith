using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeSmith.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialUsageSeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreditBalances",
                columns: table => new
                {
                    ObjectId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PaidCreditsBalance = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    FreeTokensUsedInWindow = table.Column<long>(type: "bigint", nullable: false),
                    FreeQuotaMax = table.Column<long>(type: "bigint", nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditBalances", x => x.ObjectId);
                });

            migrationBuilder.CreateTable(
                name: "IpFreeUsages",
                columns: table => new
                {
                    Ip = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FreeTokensIssued = table.Column<long>(type: "bigint", nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpFreeUsages", x => x.Ip);
                });

            migrationBuilder.CreateTable(
                name: "UsageLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObjectId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    CostUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Feature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageLedgerEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditBalances_ObjectId",
                table: "CreditBalances",
                column: "ObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_IpFreeUsages_Ip",
                table: "IpFreeUsages",
                column: "Ip");

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedgerEntries_ObjectId_TimestampUtc",
                table: "UsageLedgerEntries",
                columns: new[] { "ObjectId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedgerEntries_TimestampUtc",
                table: "UsageLedgerEntries",
                column: "TimestampUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditBalances");

            migrationBuilder.DropTable(
                name: "IpFreeUsages");

            migrationBuilder.DropTable(
                name: "UsageLedgerEntries");
        }
    }
}
