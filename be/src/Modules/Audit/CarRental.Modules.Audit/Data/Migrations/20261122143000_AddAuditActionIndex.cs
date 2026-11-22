using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Modules.Audit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditActionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entries_action",
                schema: "audit",
                table: "audit_log_entries",
                column: "action");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_log_entries_action",
                schema: "audit",
                table: "audit_log_entries");
        }
    }
}
