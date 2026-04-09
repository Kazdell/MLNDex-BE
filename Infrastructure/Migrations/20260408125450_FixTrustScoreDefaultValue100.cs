using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixTrustScoreDefaultValue100 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReadingHistory_Chapter_LastChapterId",
                table: "ReadingHistory");

            migrationBuilder.AlterColumn<int>(
                name: "TrustScore",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 100,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "TrustScore",
                table: "TranslationTeam",
                type: "int",
                nullable: false,
                defaultValue: 100,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingHistory_Chapter_LastChapterId",
                table: "ReadingHistory",
                column: "LastChapterId",
                principalTable: "Chapter",
                principalColumn: "ChapterId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReadingHistory_Chapter_LastChapterId",
                table: "ReadingHistory");

            migrationBuilder.AlterColumn<int>(
                name: "TrustScore",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 100);

            migrationBuilder.AlterColumn<int>(
                name: "TrustScore",
                table: "TranslationTeam",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 100);

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingHistory_Chapter_LastChapterId",
                table: "ReadingHistory",
                column: "LastChapterId",
                principalTable: "Chapter",
                principalColumn: "ChapterId",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
