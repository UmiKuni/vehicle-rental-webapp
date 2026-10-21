using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Modules.Pricing.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Pricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pricing");

            migrationBuilder.CreateTable(
                name: "pricing_plan_versions",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_plan_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pricing_plans",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weekday_rate_amount = table.Column<long>(type: "bigint", nullable: false),
                    weekday_rate_currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    weekend_rate_amount = table.Column<long>(type: "bigint", nullable: false),
                    weekend_rate_currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    deposit_amount = table.Column<long>(type: "bigint", nullable: false),
                    deposit_currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    km_limit = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fee_rules",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pricing_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fee_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_fee_rules_pricing_plans_pricing_plan_id",
                        column: x => x.pricing_plan_id,
                        principalSchema: "pricing",
                        principalTable: "pricing_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fee_rules_pricing_plan_id_type",
                schema: "pricing",
                table: "fee_rules",
                columns: new[] { "pricing_plan_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pricing_plan_versions_vehicle_id",
                schema: "pricing",
                table: "pricing_plan_versions",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_plans_vehicle_id",
                schema: "pricing",
                table: "pricing_plans",
                column: "vehicle_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fee_rules",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "pricing_plan_versions",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "pricing_plans",
                schema: "pricing");
        }
    }
}
