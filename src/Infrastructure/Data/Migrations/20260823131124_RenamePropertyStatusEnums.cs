using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PIPDC.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenamePropertyStatusEnums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Properties\" SET \"Status\" = 'Rented' WHERE \"Status\" = 'Leased'");
            migrationBuilder.Sql("UPDATE \"Properties\" SET \"Status\" = 'Unavailable' WHERE \"Status\" = 'Withdrawn'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Properties\" SET \"Status\" = 'Leased' WHERE \"Status\" = 'Rented'");
            migrationBuilder.Sql("UPDATE \"Properties\" SET \"Status\" = 'Withdrawn' WHERE \"Status\" = 'Unavailable'");
        }
    }
}
