using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PIPDC.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentPhotoPublicIdAndDevelopmentUpdateImagePublicIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "ImagePublicIds",
                table: "DevelopmentUpdates",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoPublicId",
                table: "Agents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagePublicIds",
                table: "DevelopmentUpdates");

            migrationBuilder.DropColumn(
                name: "PhotoPublicId",
                table: "Agents");
        }
    }
}
