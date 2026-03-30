using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationCacheMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PageTextLayer_PageId",
                table: "PageTextLayer");

            migrationBuilder.AddColumn<string>(
                name: "SourceLanguage",
                table: "PageTextLayer",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "auto");

            migrationBuilder.AddColumn<string>(
                name: "TargetLanguage",
                table: "PageTextLayer",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "vi");

            migrationBuilder.AddColumn<string>(
                name: "TranslationProvider",
                table: "PageTextLayer",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Google");

            migrationBuilder.CreateIndex(
                name: "IX_PageTextLayer_Cache",
                table: "PageTextLayer",
                columns: new[] { "PageId", "SourceLanguage", "TargetLanguage", "TranslationProvider" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PageTextLayer_Cache",
                table: "PageTextLayer");

            migrationBuilder.DropColumn(
                name: "SourceLanguage",
                table: "PageTextLayer");

            migrationBuilder.DropColumn(
                name: "TargetLanguage",
                table: "PageTextLayer");

            migrationBuilder.DropColumn(
                name: "TranslationProvider",
                table: "PageTextLayer");

            migrationBuilder.CreateIndex(
                name: "IX_PageTextLayer_PageId",
                table: "PageTextLayer",
                column: "PageId");
        }
    }
}
