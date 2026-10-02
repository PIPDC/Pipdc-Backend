using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PIPDC.src.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Adds the identity fields an agent application is now vetted on:
    /// date of birth, national identity number and stated experience.
    /// </summary>
    /// <remarks>
    /// The three columns are nullable because the table already holds rows written
    /// before these were required, and a migration must not fail on existing data.
    /// New submissions are rejected by validation when any of them is missing, so
    /// the columns settle to always-populated going forward without a backfill.
    ///
    /// NIN is deliberately not indexed or uniquely constrained. A uniqueness
    /// constraint would be a useful anti-fraud control, but applying it to a table
    /// that already contains rows with a null NIN needs a separate decision about
    /// how existing duplicate accounts are handled, and that is not this
    /// migration's call to make.
    /// </remarks>
    public partial class AddAgentApplicationIdentityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "AgentApplications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalIdentityNumber",
                table: "AgentApplications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearsOfExperience",
                table: "AgentApplications",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "AgentApplications");

            migrationBuilder.DropColumn(
                name: "NationalIdentityNumber",
                table: "AgentApplications");

            migrationBuilder.DropColumn(
                name: "YearsOfExperience",
                table: "AgentApplications");
        }
    }
}
