using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Modules.Reporting.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingProjectionRefundedAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "refunded_amount",
                schema: "reporting",
                table: "booking_report_projections",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "refunded_amount",
                schema: "reporting",
                table: "booking_report_projections");
        }
    }
}
