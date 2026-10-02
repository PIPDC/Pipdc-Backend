using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PIPDC.src.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentReportsReviewsAndSuspensionAdminAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SuspendedByAdminId",
                table: "Agents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgentId = table.Column<int>(type: "integer", nullable: false),
                    ReporterUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByAdminId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentReports_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentReports_AspNetUsers_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentReports_AspNetUsers_ReviewedByAdminId",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AgentReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgentId = table.Column<int>(type: "integer", nullable: false),
                    ReviewerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentReviews", x => x.Id);
                    table.CheckConstraint("CK_AgentReviews_Rating_Range", "\"Rating\" >= 1 AND \"Rating\" <= 5");
                    table.ForeignKey(
                        name: "FK_AgentReviews_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentReviews_AspNetUsers_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agents_SuspendedByAdminId",
                table: "Agents",
                column: "SuspendedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentReports_AgentId",
                table: "AgentReports",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentReports_AgentId_ReporterUserId_Open",
                table: "AgentReports",
                columns: new[] { "AgentId", "ReporterUserId" },
                unique: true,
                filter: "\"Status\" IN ('Open', 'UnderReview')");

            migrationBuilder.CreateIndex(
                name: "IX_AgentReports_ReporterUserId",
                table: "AgentReports",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentReports_ReviewedByAdminId",
                table: "AgentReports",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentReports_Status_CreatedAt",
                table: "AgentReports",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentReviews_AgentId",
                table: "AgentReviews",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentReviews_AgentId_ReviewerUserId",
                table: "AgentReviews",
                columns: new[] { "AgentId", "ReviewerUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentReviews_ReviewerUserId",
                table: "AgentReviews",
                column: "ReviewerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Agents_AspNetUsers_SuspendedByAdminId",
                table: "Agents",
                column: "SuspendedByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Agents_AspNetUsers_SuspendedByAdminId",
                table: "Agents");

            migrationBuilder.DropTable(
                name: "AgentReports");

            migrationBuilder.DropTable(
                name: "AgentReviews");

            migrationBuilder.DropIndex(
                name: "IX_Agents_SuspendedByAdminId",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "SuspendedByAdminId",
                table: "Agents");
        }
    }
}
