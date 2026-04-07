using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixChapterUnlockTranslationFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChapterUnlock_Translation_TranslationId",
                table: "ChapterUnlock");

            migrationBuilder.AddForeignKey(
                name: "FK_ChapterUnlock_Translation_TranslationId",
                table: "ChapterUnlock",
                column: "TranslationId",
                principalTable: "Translation",
                principalColumn: "TranslationId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChapterUnlock_Translation_TranslationId",
                table: "ChapterUnlock");

            migrationBuilder.AddForeignKey(
                name: "FK_ChapterUnlock_Translation_TranslationId",
                table: "ChapterUnlock",
                column: "TranslationId",
                principalTable: "Translation",
                principalColumn: "TranslationId");
        }
    }
}
