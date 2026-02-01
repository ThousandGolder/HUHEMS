using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HEMS.Migrations
{
    /// <inheritdoc />
    public partial class AddBannedColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Banned",
                table: "StudentExams",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Banned",
                table: "StudentExams");
        }
    }
}
