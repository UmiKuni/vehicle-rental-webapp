using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Modules.Reporting.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingReportProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "booking_report_projections",
                schema: "reporting",
                columns: table => new
                {
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_plate = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    vehicle_model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    start_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rent_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_report_projections", x => x.booking_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_booking_report_projections_owner_id",
                schema: "reporting",
                table: "booking_report_projections",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_report_projections_owner_id_start_time",
                schema: "reporting",
                table: "booking_report_projections",
                columns: new[] { "owner_id", "start_time" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_report_projections_status",
                schema: "reporting",
                table: "booking_report_projections",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_report_projections",
                schema: "reporting");
        }
    }
}
