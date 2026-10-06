using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PIPDC.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignModelWithCurrentEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PropertyImages_PropertyId",
                table: "PropertyImages");

            migrationBuilder.RenameColumn(
                name: "SizeInSqM",
                table: "Properties",
                newName: "Size");

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "PropertyImages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<List<string>>(
                name: "Amenities",
                table: "Properties",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "Properties",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Properties",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Properties",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Featured",
                table: "Properties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Properties",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Properties",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LotSize",
                table: "Properties",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Period",
                table: "Properties",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SizeUnit",
                table: "Properties",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Properties",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "YearBuilt",
                table: "Properties",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "Agents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Agents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyImages_PropertyId_DisplayOrder",
                table: "PropertyImages",
                columns: new[] { "PropertyId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_City",
                table: "Properties",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_CreatedByUserId",
                table: "Properties",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Featured",
                table: "Properties",
                column: "Featured");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_ListingType_Status",
                table: "Properties",
                columns: new[] { "ListingType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Slug",
                table: "Properties",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Properties_State",
                table: "Properties",
                column: "State");

            migrationBuilder.AddForeignKey(
                name: "FK_Properties_AspNetUsers_CreatedByUserId",
                table: "Properties",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Properties_AspNetUsers_CreatedByUserId",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_PropertyImages_PropertyId_DisplayOrder",
                table: "PropertyImages");

            migrationBuilder.DropIndex(
                name: "IX_Properties_City",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_Properties_CreatedByUserId",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_Properties_Featured",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_Properties_ListingType_Status",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_Properties_Slug",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_Properties_State",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "PropertyImages");

            migrationBuilder.DropColumn(
                name: "Amenities",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Area",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Featured",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "LotSize",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Period",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "SizeUnit",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "YearBuilt",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Agents");

            migrationBuilder.RenameColumn(
                name: "Size",
                table: "Properties",
                newName: "SizeInSqM");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyImages_PropertyId",
                table: "PropertyImages",
                column: "PropertyId");
        }
    }
}
