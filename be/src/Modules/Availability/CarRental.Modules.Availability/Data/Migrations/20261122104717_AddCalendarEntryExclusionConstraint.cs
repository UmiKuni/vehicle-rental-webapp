using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Modules.Availability.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEntryExclusionConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql(
                "DELETE FROM availability.calendar_entries WHERE type = 'Hold' AND (expires_at IS NULL OR expires_at <= NOW());");
            migrationBuilder.Sql(
                """
                ALTER TABLE availability.calendar_entries
                ADD CONSTRAINT "EX_calendar_entries_vehicle_timerange"
                EXCLUDE USING gist (
                    vehicle_id WITH =,
                    tstzrange(range_start, range_end, '[)') WITH &&
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE availability.calendar_entries
                DROP CONSTRAINT IF EXISTS "EX_calendar_entries_vehicle_timerange";
                """);

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
