using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTranslationPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<int>(
                name: "LanguageId",
                table: "TranslationPermission",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "TranslationPermission",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "REQUESTED_BY_TEAM");

            migrationBuilder.CreateIndex(
                name: "IX_TranslationPermission_LanguageId",
                table: "TranslationPermission",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_TranslationPermission_TeamId_SeriesId_LanguageId",
                table: "TranslationPermission",
                columns: new[] { "TeamId", "SeriesId", "LanguageId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TranslationPermission_Language_LanguageId",
                table: "TranslationPermission",
                column: "LanguageId",
                principalTable: "Language",
                principalColumn: "LanguageId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TranslationPermission_Language_LanguageId",
                table: "TranslationPermission");

            migrationBuilder.DropIndex(
                name: "IX_TranslationPermission_LanguageId",
                table: "TranslationPermission");

            migrationBuilder.DropIndex(
                name: "IX_TranslationPermission_TeamId_SeriesId_LanguageId",
                table: "TranslationPermission");

            migrationBuilder.DropColumn(
                name: "LanguageId",
                table: "TranslationPermission");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "TranslationPermission");
        }
    }
}
