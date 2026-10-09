using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PIPDC.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConciergeEscalations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConciergeEscalations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AiChatSessionId = table.Column<int>(type: "integer", nullable: false),
                    EscalationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EscalationStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Escalated"),
                    EscalatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedAdminId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConciergeEscalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConciergeEscalations_AiChatSessions_AiChatSessionId",
                        column: x => x.AiChatSessionId,
                        principalTable: "AiChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConciergeEscalations_AspNetUsers_AssignedAdminId",
                        column: x => x.AssignedAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConciergeEscalations_AspNetUsers_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConciergeEscalations_AiChatSessionId",
                table: "ConciergeEscalations",
                column: "AiChatSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConciergeEscalations_AssignedAdminId",
                table: "ConciergeEscalations",
                column: "AssignedAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ConciergeEscalations_EscalationStatus",
                table: "ConciergeEscalations",
                column: "EscalationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ConciergeEscalations_ResolvedByUserId",
                table: "ConciergeEscalations",
                column: "ResolvedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConciergeEscalations");
        }
    }
}
