using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeSmith.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFreeTokenWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstSeenUtc",
                table: "CreditBalances");

            migrationBuilder.RenameColumn(
                name: "FreeTokensUsedInWindow",
                table: "CreditBalances",
                newName: "FreeTokensUsed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FreeTokensUsed",
                table: "CreditBalances",
                newName: "FreeTokensUsedInWindow");

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstSeenUtc",
                table: "CreditBalances",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
