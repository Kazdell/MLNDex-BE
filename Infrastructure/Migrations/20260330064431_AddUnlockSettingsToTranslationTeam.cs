using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
  /// <inheritdoc />
  public partial class AddUnlockSettingsToTranslationTeam : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<int>(
          name: "DefaultFreeAfterDays",
          table: "TranslationTeam",
          type: "int",
          nullable: true);

      migrationBuilder.AddColumn<int>(
          name: "DefaultUnlockPriceCoins",
          table: "TranslationTeam",
          type: "int",
          nullable: true);

      migrationBuilder.AddColumn<bool>(
          name: "FreeAfterEnabled",
          table: "TranslationTeam",
          type: "bit",
          nullable: false,
          defaultValue: false);

      migrationBuilder.AddColumn<bool>(
          name: "UnlockEnabled",
          table: "TranslationTeam",
          type: "bit",
          nullable: false,
          defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(
          name: "DefaultFreeAfterDays",
          table: "TranslationTeam");

      migrationBuilder.DropColumn(
          name: "DefaultUnlockPriceCoins",
          table: "TranslationTeam");

      migrationBuilder.DropColumn(
          name: "FreeAfterEnabled",
          table: "TranslationTeam");

      migrationBuilder.DropColumn(
          name: "UnlockEnabled",
          table: "TranslationTeam");
    }
  }
}
