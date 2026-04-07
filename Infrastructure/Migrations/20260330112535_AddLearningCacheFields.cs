using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
  /// <inheritdoc />
  public partial class AddLearningCacheFields : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<int>(
          name: "AdjustedByUserId",
          table: "PageTextLayer",
          type: "int",
          nullable: true);

      migrationBuilder.AddColumn<int>(
          name: "AdjustmentCount",
          table: "PageTextLayer",
          type: "int",
          nullable: false,
          defaultValue: 0);

      migrationBuilder.AddColumn<bool>(
          name: "IsUserAdjusted",
          table: "PageTextLayer",
          type: "bit",
          nullable: false,
          defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(
          name: "AdjustedByUserId",
          table: "PageTextLayer");

      migrationBuilder.DropColumn(
          name: "AdjustmentCount",
          table: "PageTextLayer");

      migrationBuilder.DropColumn(
          name: "IsUserAdjusted",
          table: "PageTextLayer");
    }
  }
}
