using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Modules.Handover.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Handover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "handover");

            migrationBuilder.CreateTable(
                name: "handover_contexts",
                schema: "handover",
                columns: table => new
                {
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_handover_contexts", x => x.booking_id);
                });

            migrationBuilder.CreateTable(
                name: "handovers",
                schema: "handover",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    server_received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    confirmed_by_customer_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by_owner_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_handovers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "handover_photos",
                schema: "handover",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    handover_id = table.Column<Guid>(type: "uuid", nullable: false),
                    corner = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    client_captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_handover_photos", x => x.id);
                    table.ForeignKey(
                        name: "FK_handover_photos_handovers_handover_id",
                        column: x => x.handover_id,
                        principalSchema: "handover",
                        principalTable: "handovers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_conditions",
                schema: "handover",
                columns: table => new
                {
                    handover_id = table.Column<Guid>(type: "uuid", nullable: false),
                    odometer = table.Column<int>(type: "integer", nullable: false),
                    fuel_level = table.Column<int>(type: "integer", nullable: false),
                    damage_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_conditions", x => x.handover_id);
                    table.ForeignKey(
                        name: "FK_vehicle_conditions_handovers_handover_id",
                        column: x => x.handover_id,
                        principalSchema: "handover",
                        principalTable: "handovers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_handover_photos_handover_id",
                schema: "handover",
                table: "handover_photos",
                column: "handover_id");

            migrationBuilder.CreateIndex(
                name: "IX_handovers_booking_id_type",
                schema: "handover",
                table: "handovers",
                columns: new[] { "booking_id", "type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "handover_contexts",
                schema: "handover");

            migrationBuilder.DropTable(
                name: "handover_photos",
                schema: "handover");

            migrationBuilder.DropTable(
                name: "vehicle_conditions",
                schema: "handover");

            migrationBuilder.DropTable(
                name: "handovers",
                schema: "handover");
        }
    }
}
