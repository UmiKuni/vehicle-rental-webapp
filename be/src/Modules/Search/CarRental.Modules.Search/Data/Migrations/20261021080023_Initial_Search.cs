using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Modules.Search.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Search : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "search");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "vehicle_search_documents",
                schema: "search",
                columns: table => new
                {
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_lat = table.Column<double>(type: "double precision", nullable: false),
                    location_lon = table.Column<double>(type: "double precision", nullable: false),
                    brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    seats = table.Column<int>(type: "integer", nullable: false),
                    transmission = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    price_from = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    average_rating = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    total_reviews = table.Column<int>(type: "integer", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    last_fleet_event_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_pricing_event_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_rating_event_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_search_documents", x => x.vehicle_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicle_search_documents",
                schema: "search");
        }
    }
}
