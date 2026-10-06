using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PIPDC.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDevelopmentProjectDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DevelopmentProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Developer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExpectedCompletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProgressPercentage = table.Column<int>(type: "integer", nullable: false),
                    Featured = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevelopmentProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DevelopmentProjectImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DevelopmentProjectId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PublicId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsCover = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevelopmentProjectImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DevelopmentProjectImages_DevelopmentProjects_DevelopmentPro~",
                        column: x => x.DevelopmentProjectId,
                        principalTable: "DevelopmentProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DevelopmentUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DevelopmentProjectId = table.Column<int>(type: "integer", nullable: false),
                    UnitIdentifier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UnitType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevelopmentUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DevelopmentUnits_DevelopmentProjects_DevelopmentProjectId",
                        column: x => x.DevelopmentProjectId,
                        principalTable: "DevelopmentProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DevelopmentUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DevelopmentProjectId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ProgressPercentage = table.Column<int>(type: "integer", nullable: true),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ImageUrls = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevelopmentUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DevelopmentUpdates_DevelopmentProjects_DevelopmentProjectId",
                        column: x => x.DevelopmentProjectId,
                        principalTable: "DevelopmentProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DevelopmentTrackings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    DevelopmentProjectId = table.Column<int>(type: "integer", nullable: false),
                    DevelopmentUnitId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevelopmentTrackings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DevelopmentTrackings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DevelopmentTrackings_DevelopmentProjects_DevelopmentProject~",
                        column: x => x.DevelopmentProjectId,
                        principalTable: "DevelopmentProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DevelopmentTrackings_DevelopmentUnits_DevelopmentUnitId",
                        column: x => x.DevelopmentUnitId,
                        principalTable: "DevelopmentUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DevelopmentProjectImages_DevelopmentProjectId_DisplayOrder",
                table: "DevelopmentProjectImages",
                columns: new[] { "DevelopmentProjectId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DevelopmentProjects_Featured",
                table: "DevelopmentProjects",
                column: "Featured");

            migrationBuilder.CreateIndex(
                name: "IX_DevelopmentProjects_Slug",
                table: "DevelopmentProjects",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DevelopmentProjects_Status",
                table: "DevelopmentProjects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DevelopmentTrackings_DevelopmentProjectId",
                table: "DevelopmentTrackings",
                column: "DevelopmentProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DevelopmentTrackings_DevelopmentUnitId",
                table: "DevelopmentTrackings",
                column: "DevelopmentUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_DevelopmentTrackings_UserId",
                table: "DevelopmentTrackings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DevelopmentTrackings_UserId_DevelopmentProjectId",
                table: "DevelopmentTrackings",
                columns: new[] { "UserId", "DevelopmentProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DevelopmentUnits_DevelopmentProjectId_UnitIdentifier",
                table: "DevelopmentUnits",
                columns: new[] { "DevelopmentProjectId", "UnitIdentifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DevelopmentUpdates_DevelopmentProjectId_UpdateDate",
                table: "DevelopmentUpdates",
                columns: new[] { "DevelopmentProjectId", "UpdateDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DevelopmentProjectImages");

            migrationBuilder.DropTable(
                name: "DevelopmentTrackings");

            migrationBuilder.DropTable(
                name: "DevelopmentUpdates");

            migrationBuilder.DropTable(
                name: "DevelopmentUnits");

            migrationBuilder.DropTable(
                name: "DevelopmentProjects");
        }
    }
}
