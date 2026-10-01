using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PIPDC.src.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Batch1_Foundation_SuspensionTransactionsApplicationsAndLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Locations_Name",
                table: "Locations");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "SaleRecords",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "LeaseRecords",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<bool>(
                name: "IsSuspended",
                table: "Agents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                table: "Agents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionReason",
                table: "Agents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StateOfOrigin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResidentialAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LocalGovernmentArea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AgencyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AdditionalNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByAdminId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentApplications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Name_Root",
                table: "Locations",
                column: "Name",
                unique: true,
                filter: "\"ParentId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_IsSuspended",
                table: "Agents",
                column: "IsSuspended");

            migrationBuilder.CreateIndex(
                name: "IX_AgentApplications_Status",
                table: "AgentApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AgentApplications_UserId_Open",
                table: "AgentApplications",
                column: "UserId",
                unique: true,
                filter: "\"Status\" IN ('Submitted', 'UnderReview')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentApplications");

            migrationBuilder.DropIndex(
                name: "IX_Locations_Name_Root",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Agents_IsSuspended",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SaleRecords");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LeaseRecords");

            migrationBuilder.DropColumn(
                name: "IsSuspended",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "SuspensionReason",
                table: "Agents");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Name",
                table: "Locations",
                column: "Name",
                unique: true);
        }
    }
}
