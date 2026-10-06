using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PIPDC.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddXminConcurrencyTokens : Migration
    {
        /// <summary>
        /// Intentionally empty: the four protected tables already carry PostgreSQL's
        /// implicit <c>xmin</c> system column, so no DDL is required. This migration
        /// only records the row-version shadow property in the model snapshot so
        /// future <c>dotnet ef migrations add</c> runs see it as current.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
