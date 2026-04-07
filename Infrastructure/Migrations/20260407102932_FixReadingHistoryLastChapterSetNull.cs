using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixReadingHistoryLastChapterSetNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReadingHistory_Chapter_LastChapterId",
                table: "ReadingHistory");

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingHistory_Chapter_LastChapterId",
                table: "ReadingHistory",
                column: "LastChapterId",
                principalTable: "Chapter",
                principalColumn: "ChapterId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReadingHistory_Chapter_LastChapterId",
                table: "ReadingHistory");

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingHistory_Chapter_LastChapterId",
                table: "ReadingHistory",
                column: "LastChapterId",
                principalTable: "Chapter",
                principalColumn: "ChapterId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
