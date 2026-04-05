using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationToChapterUnlock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChapterUnlock_TransactionId",
                table: "ChapterUnlock");

            migrationBuilder.AlterColumn<int>(
                name: "TransactionId",
                table: "ChapterUnlock",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ChapterUnlock",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "TranslationId",
                table: "ChapterUnlock",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChapterUnlock_TransactionId",
                table: "ChapterUnlock",
                column: "TransactionId",
                unique: true,
                filter: "[TransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChapterUnlock_TranslationId",
                table: "ChapterUnlock",
                column: "TranslationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChapterUnlock_Translation_TranslationId",
                table: "ChapterUnlock",
                column: "TranslationId",
                principalTable: "Translation",
                principalColumn: "TranslationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChapterUnlock_Translation_TranslationId",
                table: "ChapterUnlock");

            migrationBuilder.DropIndex(
                name: "IX_ChapterUnlock_TransactionId",
                table: "ChapterUnlock");

            migrationBuilder.DropIndex(
                name: "IX_ChapterUnlock_TranslationId",
                table: "ChapterUnlock");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ChapterUnlock");

            migrationBuilder.DropColumn(
                name: "TranslationId",
                table: "ChapterUnlock");

            migrationBuilder.AlterColumn<int>(
                name: "TransactionId",
                table: "ChapterUnlock",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChapterUnlock_TransactionId",
                table: "ChapterUnlock",
                column: "TransactionId",
                unique: true);
        }
    }
}
