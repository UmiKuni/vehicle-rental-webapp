using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Modules.Availability.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEntryReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reason",
                schema: "availability",
                table: "calendar_entries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reason",
                schema: "availability",
                table: "calendar_entries");
        }
    }
}
