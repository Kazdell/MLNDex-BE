using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationAuthorCommissionPercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only add the new column - all other schema changes already exist in the database.
            migrationBuilder.AddColumn<decimal>(
                name: "TranslationAuthorCommissionPercent",
                table: "SystemConfigs",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 70m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TranslationAuthorCommissionPercent",
                table: "SystemConfigs");
        }
    }
}
