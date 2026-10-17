using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PIPDC.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationPropertyAndNullableEnquiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Enquiries_EnquiryId",
                table: "Conversations");

        migrationBuilder.AlterColumn<int>(
            name: "EnquiryId",
            table: "Conversations",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        // Added nullable first so existing rows start as NULL rather than 0, then
        // backfilled from the enquiry each conversation was created from (every
        // pre-existing conversation has one). Only then is it made NOT NULL and the
        // unique (client, agent, property) index created — creating that index
        // before the backfill would collide every row on PropertyId = 0.
        migrationBuilder.AddColumn<int>(
            name: "PropertyId",
            table: "Conversations",
            type: "integer",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE "Conversations" AS c
            SET "PropertyId" = e."PropertyId"
            FROM "Enquiries" AS e
            WHERE c."EnquiryId" = e."Id";
            """);

        migrationBuilder.AlterColumn<int>(
            name: "PropertyId",
            table: "Conversations",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Conversations_ClientUserId_AgentId_PropertyId",
            table: "Conversations",
            columns: new[] { "ClientUserId", "AgentId", "PropertyId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Conversations_PropertyId",
            table: "Conversations",
            column: "PropertyId");

        migrationBuilder.AddForeignKey(
            name: "FK_Conversations_Enquiries_EnquiryId",
            table: "Conversations",
            column: "EnquiryId",
            principalTable: "Enquiries",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_Conversations_Properties_PropertyId",
            table: "Conversations",
            column: "PropertyId",
            principalTable: "Properties",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Enquiries_EnquiryId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Properties_PropertyId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ClientUserId_AgentId_PropertyId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_PropertyId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "Conversations");

            migrationBuilder.AlterColumn<int>(
                name: "EnquiryId",
                table: "Conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Enquiries_EnquiryId",
                table: "Conversations",
                column: "EnquiryId",
                principalTable: "Enquiries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
