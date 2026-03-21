using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TranslationEcosystemUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOfficial",
                table: "Translation",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOrphan",
                table: "Translation",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOutdated",
                table: "Translation",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TranslationCredit",
                columns: table => new
                {
                    TranslationId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationCredit", x => new { x.TranslationId, x.UserId, x.Role });
                    table.ForeignKey(
                        name: "FK_TranslationCredit_Translation_TranslationId",
                        column: x => x.TranslationId,
                        principalTable: "Translation",
                        principalColumn: "TranslationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TranslationCredit_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TranslationTeamJoin",
                columns: table => new
                {
                    TranslationId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationTeamJoin", x => new { x.TranslationId, x.TeamId });
                    table.ForeignKey(
                        name: "FK_TranslationTeamJoin_TranslationTeam_TeamId",
                        column: x => x.TeamId,
                        principalTable: "TranslationTeam",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TranslationTeamJoin_Translation_TranslationId",
                        column: x => x.TranslationId,
                        principalTable: "Translation",
                        principalColumn: "TranslationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TranslationCredit_UserId",
                table: "TranslationCredit",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TranslationTeamJoin_TeamId",
                table: "TranslationTeamJoin",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranslationCredit");

            migrationBuilder.DropTable(
                name: "TranslationTeamJoin");

            migrationBuilder.DropColumn(
                name: "IsOfficial",
                table: "Translation");

            migrationBuilder.DropColumn(
                name: "IsOrphan",
                table: "Translation");

            migrationBuilder.DropColumn(
                name: "IsOutdated",
                table: "Translation");
        }
    }
}
