using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PIPDC.src.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentLifecycleRevocationAppealsAndBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRemoved",
                table: "Agents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReassignedToAgentId",
                table: "Agents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReinstatedAt",
                table: "Agents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemovalReason",
                table: "Agents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedAt",
                table: "Agents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemovedByAdminId",
                table: "Agents",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevocationReason",
                table: "AgentApplications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "AgentApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevokedByAdminId",
                table: "AgentApplications",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentApplicationBlocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LiftedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LiftedByAdminId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentApplicationBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentApplicationBlocks_AspNetUsers_LiftedByAdminId",
                        column: x => x.LiftedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AgentApplicationBlocks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentRegistrationAppeals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgentApplicationId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByAdminId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRegistrationAppeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentRegistrationAppeals_AgentApplications_AgentApplication~",
                        column: x => x.AgentApplicationId,
                        principalTable: "AgentApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AgentRegistrationAppeals_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agents_IsRemoved",
                table: "Agents",
                column: "IsRemoved");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_ReassignedToAgentId",
                table: "Agents",
                column: "ReassignedToAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_RemovedByAdminId",
                table: "Agents",
                column: "RemovedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentApplicationBlocks_LiftedByAdminId",
                table: "AgentApplicationBlocks",
                column: "LiftedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentApplicationBlocks_UserId",
                table: "AgentApplicationBlocks",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentRegistrationAppeals_AgentApplicationId",
                table: "AgentRegistrationAppeals",
                column: "AgentApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRegistrationAppeals_Status",
                table: "AgentRegistrationAppeals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRegistrationAppeals_UserId_Open",
                table: "AgentRegistrationAppeals",
                column: "UserId",
                unique: true,
                filter: "\"Status\" IN ('Submitted', 'UnderReview')");

            migrationBuilder.AddForeignKey(
                name: "FK_Agents_Agents_ReassignedToAgentId",
                table: "Agents",
                column: "ReassignedToAgentId",
                principalTable: "Agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Agents_AspNetUsers_RemovedByAdminId",
                table: "Agents",
                column: "RemovedByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Agents_Agents_ReassignedToAgentId",
                table: "Agents");

            migrationBuilder.DropForeignKey(
                name: "FK_Agents_AspNetUsers_RemovedByAdminId",
                table: "Agents");

            migrationBuilder.DropTable(
                name: "AgentApplicationBlocks");

            migrationBuilder.DropTable(
                name: "AgentRegistrationAppeals");

            migrationBuilder.DropIndex(
                name: "IX_Agents_IsRemoved",
                table: "Agents");

            migrationBuilder.DropIndex(
                name: "IX_Agents_ReassignedToAgentId",
                table: "Agents");

            migrationBuilder.DropIndex(
                name: "IX_Agents_RemovedByAdminId",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "IsRemoved",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "ReassignedToAgentId",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "ReinstatedAt",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "RemovalReason",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "RemovedByAdminId",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "RevocationReason",
                table: "AgentApplications");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "AgentApplications");

            migrationBuilder.DropColumn(
                name: "RevokedByAdminId",
                table: "AgentApplications");
        }
    }
}
