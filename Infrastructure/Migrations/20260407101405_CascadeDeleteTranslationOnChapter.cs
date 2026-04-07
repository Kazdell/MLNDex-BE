using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteTranslationOnChapter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Translation_Chapter_ChapterId",
                table: "Translation");

            migrationBuilder.AddForeignKey(
                name: "FK_Translation_Chapter_ChapterId",
                table: "Translation",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "ChapterId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Translation_Chapter_ChapterId",
                table: "Translation");

            migrationBuilder.AddForeignKey(
                name: "FK_Translation_Chapter_ChapterId",
                table: "Translation",
                column: "ChapterId",
                principalTable: "Chapter",
                principalColumn: "ChapterId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
