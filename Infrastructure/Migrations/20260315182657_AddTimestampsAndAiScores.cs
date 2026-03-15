using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTimestampsAndAiScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Certificates",
                table: "TranslationTeam");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TranslationTeam");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "TranslationTeam");

            migrationBuilder.AddColumn<string>(
                name: "AiScoresJson",
                table: "Chapter",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiScoresJson",
                table: "Chapter");

            migrationBuilder.AddColumn<string>(
                name: "Certificates",
                table: "TranslationTeam",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TranslationTeam",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "TranslationTeam",
                type: "datetime2",
                nullable: true);
        }
    }
}
