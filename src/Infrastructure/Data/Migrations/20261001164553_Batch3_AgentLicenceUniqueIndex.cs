using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PIPDC.src.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Batch3_AgentLicenceUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Agents_LicenseNumber",
                table: "Agents",
                column: "LicenseNumber",
                unique: true,
                filter: "\"LicenseNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Agents_LicenseNumber",
                table: "Agents");
        }
    }
}
