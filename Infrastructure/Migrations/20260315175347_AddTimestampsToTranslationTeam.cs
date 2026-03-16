using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTimestampsToTranslationTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Các thay đổi đã được add bằng tay lên Azure DB nên để rỗng tránh lỗi 2705 và 2714 "Đã tồn tại"
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Các rollback này cũng sẽ bỏ qua
        }
    }
}
