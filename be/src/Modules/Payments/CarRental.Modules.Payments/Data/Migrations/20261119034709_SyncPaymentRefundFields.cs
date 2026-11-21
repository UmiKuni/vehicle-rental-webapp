using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Modules.Payments.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncPaymentRefundFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "refund_reason",
                schema: "payments",
                table: "payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refund_transaction_id",
                schema: "payments",
                table: "payments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "refunded_amount",
                schema: "payments",
                table: "payments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "refunded_at",
                schema: "payments",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refunded_currency",
                schema: "payments",
                table: "payments",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "VND");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "refund_reason",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "refund_transaction_id",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "refunded_amount",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "refunded_at",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "refunded_currency",
                schema: "payments",
                table: "payments");
        }
    }
}
