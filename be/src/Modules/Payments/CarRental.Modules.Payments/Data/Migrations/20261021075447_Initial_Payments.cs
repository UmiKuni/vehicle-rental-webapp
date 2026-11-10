using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Modules.Payments.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Payments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payments");

            migrationBuilder.CreateTable(
                name: "deposits",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    deducted_amount = table.Column<long>(type: "bigint", nullable: false),
                    deducted_currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    release_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_frozen = table.Column<bool>(type: "boolean", nullable: false),
                    settled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deposits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    gateway_transaction_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    deposit_portion_amount = table.Column<long>(type: "bigint", nullable: false),
                    deposit_portion_currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_inboxes",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_inboxes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_deposits_booking_id",
                schema: "payments",
                table: "deposits",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_booking_id",
                schema: "payments",
                table: "payments",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_gateway_transaction_id",
                schema: "payments",
                table: "payments",
                column: "gateway_transaction_id",
                unique: true,
                filter: "gateway_transaction_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deposits",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "webhook_inboxes",
                schema: "payments");
        }
    }
}
