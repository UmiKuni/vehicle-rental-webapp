using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Modules.Fleet.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Fleet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fleet");

            migrationBuilder.CreateTable(
                name: "vehicle_brands",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_brands", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_models",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_models", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plate_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transmission = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    seats = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    location_lat = table.Column<double>(type: "double precision", nullable: false),
                    location_lon = table.Column<double>(type: "double precision", nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    hidden_by_admin = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_approvals",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_approvals", x => x.id);
                    table.ForeignKey(
                        name: "FK_vehicle_approvals_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalSchema: "fleet",
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_images",
                schema: "fleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_vehicle_images_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalSchema: "fleet",
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_approvals_vehicle_id",
                schema: "fleet",
                table: "vehicle_approvals",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_brands_name",
                schema: "fleet",
                table: "vehicle_brands",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_images_vehicle_id",
                schema: "fleet",
                table: "vehicle_images",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_models_brand_id",
                schema: "fleet",
                table: "vehicle_models",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_owner_id",
                schema: "fleet",
                table: "vehicles",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_plate_number",
                schema: "fleet",
                table: "vehicles",
                column: "plate_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicle_approvals",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "vehicle_brands",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "vehicle_images",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "vehicle_models",
                schema: "fleet");

            migrationBuilder.DropTable(
                name: "vehicles",
                schema: "fleet");
        }
    }
}
