using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GerenWare.Projects.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StatusReportNotesFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Format",
                schema: "projects",
                table: "status_report_notes",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                // The reports generated before there was the PDF were all PowerPoints (ADR-0165).
                defaultValue: "PowerPoint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Format",
                schema: "projects",
                table: "status_report_notes");
        }
    }
}
