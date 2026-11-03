using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Modules.Availability.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Availability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "availability");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "vehicle_calendars",
                schema: "availability",
                columns: table => new
                {
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_calendars", x => x.vehicle_id);
                });

            migrationBuilder.CreateTable(
                name: "calendar_entries",
                schema: "availability",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    range_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    range_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_calendar_entries_vehicle_calendars_vehicle_id",
                        column: x => x.vehicle_id,
                        principalSchema: "availability",
                        principalTable: "vehicle_calendars",
                        principalColumn: "vehicle_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_calendar_entries_booking_id",
                schema: "availability",
                table: "calendar_entries",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_calendar_entries_vehicle_id",
                schema: "availability",
                table: "calendar_entries",
                column: "vehicle_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_entries",
                schema: "availability");

            migrationBuilder.DropTable(
                name: "vehicle_calendars",
                schema: "availability");
        }
    }
}
