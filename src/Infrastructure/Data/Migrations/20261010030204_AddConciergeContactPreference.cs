using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PIPDC.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConciergeContactPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientPhone",
                table: "ConciergeEscalations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPreference",
                table: "ConciergeEscalations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingEscalationReason",
                table: "AiChatSessions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientPhone",
                table: "ConciergeEscalations");

            migrationBuilder.DropColumn(
                name: "ContactPreference",
                table: "ConciergeEscalations");

            migrationBuilder.DropColumn(
                name: "PendingEscalationReason",
                table: "AiChatSessions");
        }
    }
}
