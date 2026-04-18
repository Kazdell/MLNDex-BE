using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateWithdrawalRequestUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequest_CreatorProfile_CreatorId",
                table: "WithdrawalRequest");

            migrationBuilder.RenameColumn(
                name: "CreatorId",
                table: "WithdrawalRequest",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_WithdrawalRequest_CreatorId",
                table: "WithdrawalRequest",
                newName: "IX_WithdrawalRequest_UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequest_User_UserId",
                table: "WithdrawalRequest",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequest_User_UserId",
                table: "WithdrawalRequest");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "WithdrawalRequest",
                newName: "CreatorId");

            migrationBuilder.RenameIndex(
                name: "IX_WithdrawalRequest_UserId",
                table: "WithdrawalRequest",
                newName: "IX_WithdrawalRequest_CreatorId");

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequest_CreatorProfile_CreatorId",
                table: "WithdrawalRequest",
                column: "CreatorId",
                principalTable: "CreatorProfile",
                principalColumn: "CreatorId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
