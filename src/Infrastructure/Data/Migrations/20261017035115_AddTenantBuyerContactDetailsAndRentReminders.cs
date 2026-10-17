using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PIPDC.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBuyerContactDetailsAndRentReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuyerEmail",
                table: "SaleRecords",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BuyerPhone",
                table: "SaleRecords",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RentReminderSentAt",
                table: "LeaseRecords",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantEmail",
                table: "LeaseRecords",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantPhone",
                table: "LeaseRecords",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // The single free-text contact is replaced by email and phone. Carry
            // the old value over as the phone so existing records keep a contact
            // number rather than losing it to the split.
            migrationBuilder.Sql(
                "UPDATE \"SaleRecords\" SET \"BuyerPhone\" = \"BuyerContact\" WHERE \"BuyerContact\" IS NOT NULL AND \"BuyerContact\" <> '';");

            migrationBuilder.Sql(
                "UPDATE \"LeaseRecords\" SET \"TenantPhone\" = \"TenantContact\" WHERE \"TenantContact\" IS NOT NULL AND \"TenantContact\" <> '';");

            migrationBuilder.DropColumn(
                name: "BuyerContact",
                table: "SaleRecords");

            migrationBuilder.DropColumn(
                name: "TenantContact",
                table: "LeaseRecords");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuyerEmail",
                table: "SaleRecords");

            migrationBuilder.DropColumn(
                name: "BuyerPhone",
                table: "SaleRecords");

            migrationBuilder.DropColumn(
                name: "RentReminderSentAt",
                table: "LeaseRecords");

            migrationBuilder.DropColumn(
                name: "TenantEmail",
                table: "LeaseRecords");

            migrationBuilder.DropColumn(
                name: "TenantPhone",
                table: "LeaseRecords");

            migrationBuilder.AddColumn<string>(
                name: "BuyerContact",
                table: "SaleRecords",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantContact",
                table: "LeaseRecords",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
