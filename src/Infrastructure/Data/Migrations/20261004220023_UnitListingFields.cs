using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PIPDC.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnitListingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "Amenities",
                table: "DevelopmentUnits",
                type: "text[]",
                nullable: false,
                // The table already has rows, so NOT NULL needs an explicit default.
                // Without it Postgres rejects the ADD COLUMN with 23502.
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<int>(
                name: "Bathrooms",
                table: "DevelopmentUnits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Bedrooms",
                table: "DevelopmentUnits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ListingType",
                table: "DevelopmentUnits",
                type: "text",
                nullable: false,
                // Must be a real enum name: an empty string would fail to convert
                // on read and break every existing unit.
                defaultValue: "ForSale");

            migrationBuilder.AddColumn<string>(
                name: "Period",
                table: "DevelopmentUnits",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PropertyId",
                table: "DevelopmentUnits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyType",
                table: "DevelopmentUnits",
                type: "text",
                nullable: false,
                defaultValue: "Residential");

            migrationBuilder.AddColumn<double>(
                name: "Size",
                table: "DevelopmentUnits",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SizeUnit",
                table: "DevelopmentUnits",
                type: "text",
                nullable: false,
                defaultValue: "sqm");

            migrationBuilder.AddColumn<int>(
                name: "YearBuilt",
                table: "DevelopmentUnits",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DevelopmentUnits_PropertyId",
                table: "DevelopmentUnits",
                column: "PropertyId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DevelopmentUnits_Properties_PropertyId",
                table: "DevelopmentUnits",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DevelopmentUnits_Properties_PropertyId",
                table: "DevelopmentUnits");

            migrationBuilder.DropIndex(
                name: "IX_DevelopmentUnits_PropertyId",
                table: "DevelopmentUnits");

            migrationBuilder.DropColumn(
                name: "Amenities",
                table: "DevelopmentUnits");

            migrationBuilder.DropColumn(
                name: "Bathrooms",
                table: "DevelopmentUnits");

            migrationBuilder.DropColumn(
                name: "Bedrooms",
                table: "DevelopmentUnits");

            migrationBuilder.DropColumn(
                name: "ListingType",
                table: "DevelopmentUnits");

            migrationBuilder.DropColumn(
                name: "Period",
                table: "DevelopmentUnits");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "DevelopmentUnits");

            migrationBuilder.DropColumn(
                name: "PropertyType",
                table: "DevelopmentUnits");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "DevelopmentUnits");

            migrationBuilder.DropColumn(
                name: "SizeUnit",
                table: "DevelopmentUnits");

            migrationBuilder.DropColumn(
                name: "YearBuilt",
                table: "DevelopmentUnits");
        }
    }
}
