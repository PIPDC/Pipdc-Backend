using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PIPDC.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentReadAtAndEnquiryStatusUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AgentReadAt",
                table: "Enquiries",
                type: "timestamp with time zone",
                nullable: true);

            // Upgrade legacy status values to the current enumeration.
            // 'Responded' was replaced by 'InProgress'.
            migrationBuilder.Sql(
                "UPDATE \"Enquiries\" SET \"Status\" = 'InProgress' WHERE \"Status\" = 'Responded';");
            // 'Closed' was replaced by 'Resolved'.
            migrationBuilder.Sql(
                "UPDATE \"Enquiries\" SET \"Status\" = 'Resolved' WHERE \"Status\" = 'Closed';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert the status upgrade before removing the column.
            migrationBuilder.Sql(
                "UPDATE \"Enquiries\" SET \"Status\" = 'Responded' WHERE \"Status\" = 'InProgress';");
            migrationBuilder.Sql(
                "UPDATE \"Enquiries\" SET \"Status\" = 'Closed' WHERE \"Status\" = 'Resolved';");

            migrationBuilder.DropColumn(
                name: "AgentReadAt",
                table: "Enquiries");
        }
    }
}
