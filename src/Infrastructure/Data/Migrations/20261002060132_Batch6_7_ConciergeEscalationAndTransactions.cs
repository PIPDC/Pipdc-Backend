using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PIPDC.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Batch6_7_ConciergeEscalationAndTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LeaseRecords_PropertyId",
                table: "LeaseRecords");

            migrationBuilder.AddColumn<string>(
                name: "BuyerUserId",
                table: "SaleRecords",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EnquiryId",
                table: "SaleRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordedByUserId",
                table: "SaleRecords",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "EnquiryId",
                table: "LeaseRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordedByUserId",
                table: "LeaseRecords",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantUserId",
                table: "LeaseRecords",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedAdminId",
                table: "Conversations",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EscalatedAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalatedByUserId",
                table: "Conversations",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalationReason",
                table: "Conversations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalationStatus",
                table: "Conversations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedByUserId",
                table: "Conversations",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleRecords_BuyerUserId",
                table: "SaleRecords",
                column: "BuyerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRecords_EnquiryId",
                table: "SaleRecords",
                column: "EnquiryId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRecords_RecordedByUserId",
                table: "SaleRecords",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseRecords_EnquiryId",
                table: "LeaseRecords",
                column: "EnquiryId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseRecords_OneLiveLeasePerProperty",
                table: "LeaseRecords",
                column: "PropertyId",
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Active')");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseRecords_RecordedByUserId",
                table: "LeaseRecords",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseRecords_Status_LeaseEndDate",
                table: "LeaseRecords",
                columns: new[] { "Status", "LeaseEndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaseRecords_TenantUserId",
                table: "LeaseRecords",
                column: "TenantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AssignedAdminId",
                table: "Conversations",
                column: "AssignedAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_EscalatedByUserId",
                table: "Conversations",
                column: "EscalatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_EscalationStatus",
                table: "Conversations",
                column: "EscalationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ResolvedByUserId",
                table: "Conversations",
                column: "ResolvedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_AspNetUsers_AssignedAdminId",
                table: "Conversations",
                column: "AssignedAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_AspNetUsers_EscalatedByUserId",
                table: "Conversations",
                column: "EscalatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_AspNetUsers_ResolvedByUserId",
                table: "Conversations",
                column: "ResolvedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaseRecords_AspNetUsers_RecordedByUserId",
                table: "LeaseRecords",
                column: "RecordedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaseRecords_AspNetUsers_TenantUserId",
                table: "LeaseRecords",
                column: "TenantUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaseRecords_Enquiries_EnquiryId",
                table: "LeaseRecords",
                column: "EnquiryId",
                principalTable: "Enquiries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleRecords_AspNetUsers_BuyerUserId",
                table: "SaleRecords",
                column: "BuyerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleRecords_AspNetUsers_RecordedByUserId",
                table: "SaleRecords",
                column: "RecordedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleRecords_Enquiries_EnquiryId",
                table: "SaleRecords",
                column: "EnquiryId",
                principalTable: "Enquiries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_AspNetUsers_AssignedAdminId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_AspNetUsers_EscalatedByUserId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_AspNetUsers_ResolvedByUserId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaseRecords_AspNetUsers_RecordedByUserId",
                table: "LeaseRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaseRecords_AspNetUsers_TenantUserId",
                table: "LeaseRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaseRecords_Enquiries_EnquiryId",
                table: "LeaseRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleRecords_AspNetUsers_BuyerUserId",
                table: "SaleRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleRecords_AspNetUsers_RecordedByUserId",
                table: "SaleRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleRecords_Enquiries_EnquiryId",
                table: "SaleRecords");

            migrationBuilder.DropIndex(
                name: "IX_SaleRecords_BuyerUserId",
                table: "SaleRecords");

            migrationBuilder.DropIndex(
                name: "IX_SaleRecords_EnquiryId",
                table: "SaleRecords");

            migrationBuilder.DropIndex(
                name: "IX_SaleRecords_RecordedByUserId",
                table: "SaleRecords");

            migrationBuilder.DropIndex(
                name: "IX_LeaseRecords_EnquiryId",
                table: "LeaseRecords");

            migrationBuilder.DropIndex(
                name: "IX_LeaseRecords_OneLiveLeasePerProperty",
                table: "LeaseRecords");

            migrationBuilder.DropIndex(
                name: "IX_LeaseRecords_RecordedByUserId",
                table: "LeaseRecords");

            migrationBuilder.DropIndex(
                name: "IX_LeaseRecords_Status_LeaseEndDate",
                table: "LeaseRecords");

            migrationBuilder.DropIndex(
                name: "IX_LeaseRecords_TenantUserId",
                table: "LeaseRecords");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_AssignedAdminId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_EscalatedByUserId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_EscalationStatus",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ResolvedByUserId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "BuyerUserId",
                table: "SaleRecords");

            migrationBuilder.DropColumn(
                name: "EnquiryId",
                table: "SaleRecords");

            migrationBuilder.DropColumn(
                name: "RecordedByUserId",
                table: "SaleRecords");

            migrationBuilder.DropColumn(
                name: "EnquiryId",
                table: "LeaseRecords");

            migrationBuilder.DropColumn(
                name: "RecordedByUserId",
                table: "LeaseRecords");

            migrationBuilder.DropColumn(
                name: "TenantUserId",
                table: "LeaseRecords");

            migrationBuilder.DropColumn(
                name: "AssignedAdminId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "EscalatedByUserId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "EscalationReason",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "EscalationStatus",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "Conversations");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseRecords_PropertyId",
                table: "LeaseRecords",
                column: "PropertyId",
                unique: true);
        }
    }
}
