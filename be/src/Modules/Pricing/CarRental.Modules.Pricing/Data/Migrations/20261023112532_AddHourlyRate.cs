using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Modules.Pricing.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHourlyRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "hourly_rate_amount",
                schema: "pricing",
                table: "pricing_plans",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hourly_rate_currency",
                schema: "pricing",
                table: "pricing_plans",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hourly_rate_amount",
                schema: "pricing",
                table: "pricing_plans");

            migrationBuilder.DropColumn(
                name: "hourly_rate_currency",
                schema: "pricing",
                table: "pricing_plans");
        }
    }
}
