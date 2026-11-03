using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRental.Modules.Pricing.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePricingPlanXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // xmin is a PostgreSQL system column. It is not a user-defined
            // column and therefore cannot be dropped; only the EF mapping is
            // removed by this migration.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // xmin is always available on PostgreSQL tables. The previous EF
            // mapping is restored by reverting the model snapshot/configuration.
        }
    }
}
