using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthFields : Migration
    {
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AlterColumn<string>(
				name: "Email",
				table: "User",
				type: "nvarchar(256)",
				maxLength: 256,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "nvarchar(25)",
				oldMaxLength: 25);

			migrationBuilder.AlterColumn<string>(
				name: "DisplayName",
				table: "User",
				type: "nvarchar(100)",
				maxLength: 100,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "nvarchar(25)",
				oldMaxLength: 25);
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AlterColumn<string>(
				name: "Email",
				table: "User",
				type: "nvarchar(25)",
				maxLength: 25,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "nvarchar(256)",
				oldMaxLength: 256);

			migrationBuilder.AlterColumn<string>(
				name: "DisplayName",
				table: "User",
				type: "nvarchar(25)",
				maxLength: 25,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "nvarchar(100)",
				oldMaxLength: 100);
		}
	}
}
