using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Library.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameRemarkToDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF Core 看不出是改名，產生的是 DropColumn + AddColumn（資料會不見），改成 RenameColumn
            migrationBuilder.RenameColumn(
                name: "Remark",
                table: "Works",
                newName: "Description");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Works",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 超過 300 字的簡介縮回去時會失敗（SQL Server 不會自動截斷）
            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Works",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Works",
                newName: "Remark");
        }
    }
}
