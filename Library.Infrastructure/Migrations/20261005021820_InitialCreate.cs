using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Library.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Works",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TitleZh = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TitleJa = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ReleaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Favorite = table.Column<bool>(type: "bit", nullable: false),
                    Purchased = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Works", x => x.Id);
                    table.CheckConstraint("CK_Works_HasTitle", "[TitleZh] <> N'' OR [TitleJa] <> N'' OR [TitleEn] <> N''");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Works");
        }
    }
}
