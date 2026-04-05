using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
  /// <inheritdoc />
  public partial class AddTeamIdToTranslation : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {

      migrationBuilder.AddColumn<int>(
          name: "TeamId",
          table: "Translation",
          type: "int",
          nullable: true);

      migrationBuilder.CreateIndex(
          name: "IX_Translation_TeamId",
          table: "Translation",
          column: "TeamId");

      migrationBuilder.AddForeignKey(
          name: "FK_Translation_TranslationTeam_TeamId",
          table: "Translation",
          column: "TeamId",
          principalTable: "TranslationTeam",
          principalColumn: "TeamId",
          onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropForeignKey(
          name: "FK_Translation_TranslationTeam_TeamId",
          table: "Translation");

      migrationBuilder.DropIndex(
          name: "IX_Translation_TeamId",
          table: "Translation");

      migrationBuilder.DropColumn(
          name: "TeamId",
          table: "Translation");
    }
  }
}
