using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
  /// <inheritdoc />
  public partial class AddUnlockSettingsToCreatorProfile : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<int>(
          name: "DefaultFreeAfterDays",
          table: "CreatorProfile",
          type: "int",
          nullable: true);

      migrationBuilder.AddColumn<int>(
          name: "DefaultUnlockPriceCoins",
          table: "CreatorProfile",
          type: "int",
          nullable: true);

      migrationBuilder.AddColumn<bool>(
          name: "FreeAfterEnabled",
          table: "CreatorProfile",
          type: "bit",
          nullable: false,
          defaultValue: false);

      migrationBuilder.AddColumn<bool>(
          name: "UnlockEnabled",
          table: "CreatorProfile",
          type: "bit",
          nullable: false,
          defaultValue: false);

      //migrationBuilder.CreateTable(
      //    name: "CoinRateSetting",
      //    columns: table => new
      //    {
      //        Id = table.Column<int>(type: "int", nullable: false)
      //            .Annotation("SqlServer:Identity", "1, 1"),
      //        CoinsPerVnd = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
      //        MinTopUpVnd = table.Column<long>(type: "bigint", nullable: false),
      //        MaxTopUpVnd = table.Column<long>(type: "bigint", nullable: false),
      //        IsActive = table.Column<bool>(type: "bit", nullable: false),
      //        UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
      //        UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
      //        Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
      //    },
      //    constraints: table =>
      //    {
      //        table.PrimaryKey("PK_CoinRateSetting", x => x.Id);
      //        table.ForeignKey(
      //            name: "FK_CoinRateSetting_User_UpdatedByUserId",
      //            column: x => x.UpdatedByUserId,
      //            principalTable: "User",
      //            principalColumn: "UserId",
      //            onDelete: ReferentialAction.Restrict);
      //    });

      //migrationBuilder.CreateIndex(
      //    name: "IX_CoinRateSetting_UpdatedByUserId",
      //    table: "CoinRateSetting",
      //    column: "UpdatedByUserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "CoinRateSetting");

      migrationBuilder.DropColumn(
          name: "DefaultFreeAfterDays",
          table: "CreatorProfile");

      migrationBuilder.DropColumn(
          name: "DefaultUnlockPriceCoins",
          table: "CreatorProfile");

      migrationBuilder.DropColumn(
          name: "FreeAfterEnabled",
          table: "CreatorProfile");

      migrationBuilder.DropColumn(
          name: "UnlockEnabled",
          table: "CreatorProfile");
    }
  }
}
