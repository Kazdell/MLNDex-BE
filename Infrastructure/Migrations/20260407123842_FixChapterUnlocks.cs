using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixChapterUnlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChapterUnlock_UserId",
                table: "ChapterUnlock");

            migrationBuilder.CreateIndex(
                name: "IX_ChapterUnlock_UserId_ChapterId_TranslationId",
                table: "ChapterUnlock",
                columns: new[] { "UserId", "ChapterId", "TranslationId" },
                unique: true,
                filter: "[TranslationId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChapterUnlock_UserId_ChapterId_TranslationId",
                table: "ChapterUnlock");

            migrationBuilder.CreateIndex(
                name: "IX_ChapterUnlock_UserId",
                table: "ChapterUnlock",
                column: "UserId");
        }
    }
}
