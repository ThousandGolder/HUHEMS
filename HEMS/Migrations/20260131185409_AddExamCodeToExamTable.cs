using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HEMS.Migrations
{
    /// <inheritdoc />
    public partial class AddExamCodeToExamTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExamCode",
                table: "Exams",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExamCode",
                table: "Exams");
        }
    }
}
