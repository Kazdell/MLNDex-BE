using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrustScoreEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TrustScore",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TrustScore",
                table: "TranslationTeam",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "QueueId",
                table: "Report",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "EvidenceUrlsJson",
                table: "Report",
                type: "nvarchar(MAX)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Report",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateTable(
                name: "TrustScoreHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    TranslationTeamId = table.Column<int>(type: "int", nullable: true),
                    ScoreChange = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RelatedReportId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustScoreHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrustScoreHistories_Report_RelatedReportId",
                        column: x => x.RelatedReportId,
                        principalTable: "Report",
                        principalColumn: "ReportId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TrustScoreHistories_TranslationTeam_TranslationTeamId",
                        column: x => x.TranslationTeamId,
                        principalTable: "TranslationTeam",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrustScoreHistories_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrustScoreHistories_RelatedReportId",
                table: "TrustScoreHistories",
                column: "RelatedReportId");

            migrationBuilder.CreateIndex(
                name: "IX_TrustScoreHistories_TranslationTeamId",
                table: "TrustScoreHistories",
                column: "TranslationTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TrustScoreHistories_UserId",
                table: "TrustScoreHistories",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrustScoreHistories");

            migrationBuilder.DropColumn(
                name: "TrustScore",
                table: "User");

            migrationBuilder.DropColumn(
                name: "TrustScore",
                table: "TranslationTeam");

            migrationBuilder.DropColumn(
                name: "EvidenceUrlsJson",
                table: "Report");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Report");

            migrationBuilder.AlterColumn<int>(
                name: "QueueId",
                table: "Report",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
