using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Modules.Reporting.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Reporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.CreateTable(
                name: "daily_booking_stats",
                schema: "reporting",
                columns: table => new
                {
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_bookings = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    total_revenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    cancellation_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_booking_stats", x => x.date);
                });

            migrationBuilder.CreateTable(
                name: "processed_events",
                schema: "reporting",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_events", x => x.event_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_booking_stats",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "processed_events",
                schema: "reporting");
        }
    }
}
