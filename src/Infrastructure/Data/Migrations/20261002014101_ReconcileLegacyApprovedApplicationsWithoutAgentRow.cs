using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PIPDC.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Repairs applications that the pre-lifecycle role-removal path left in an
    /// impossible state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>AuthService.RemoveRoleAsync</c> hard-deleted the <c>Agent</c> row and removed the
    /// Identity role, but never touched the <c>AgentApplication</c>. The application therefore
    /// stayed <c>Approved</c> forever, so the applicant kept being told their licence had been
    /// issued and shown in their agent dashboard while every agent screen refused them.
    /// </para>
    /// <para>
    /// This recreates the deleted <c>Agent</c> row as a REMOVED agent and marks the
    /// application <c>Revoked</c>, which is exactly the state <c>AgentService.RemoveAsync</c>
    /// produces for the same situation. Restoring the row rather than only relabelling the
    /// application matters: without an <c>Agent</c> row the appeal uphold path returns
    /// <c>agentappeal.agentgone</c>, so the applicant could lodge an appeal that no
    /// administrator could ever grant.
    /// </para>
    /// <para>
    /// The scope is the invariant, not the account: any <c>Approved</c> application whose user has
    /// no <c>Agent</c> row is stale by construction, because approval is what creates that row. On
    /// a database that never had a legacy demotion this matches nothing and is a no-op.
    /// </para>
    /// <para>
    /// <c>LicenseNumber</c> is intentionally left NULL. It lived only on the deleted row, so the
    /// number that was emailed can no longer be reproduced from the database, and inventing one
    /// would assert a licence the system never issued. A removed agent has no active licence
    /// anyway.
    /// </para>
    /// </remarks>
    public partial class ReconcileLegacyApprovedApplicationsWithoutAgentRow : Migration
    {
        /// <summary>
        /// Shared by Up and Down so the marker this migration writes is the same marker it
        /// recognises. It doubles as the LIKE prefix that ties both the revoke and the rollback
        /// to rows this migration itself created, so a genuine registration that happens to be a
        /// removed agent with no licence is never touched.
        /// </summary>
        private const string Reason =
            "Agent role was removed by an administrator before the revocation lifecycle existed, " +
            "which deleted the agent record but left the application approved. Record reconciled on 2026-10-02.";

        private const string MarkerPrefix =
            "Agent role was removed by an administrator before the revocation lifecycle existed%";

        /// <summary>
        /// Renders a value as a quoted SQL string literal. <c>MigrationBuilder.Sql</c> in this EF
        /// version has no parameterised overload, so the text is inlined; this keeps the escaping
        /// correct if the wording ever gains an apostrophe.
        /// </summary>
        private static string Literal(string value) => "'" + value.Replace("'", "''") + "'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Attributed to the administrator who reviewed the application. COALESCE falls back to
            // the earliest admin so the foreign key can never fail on a null reviewer.
            migrationBuilder.Sql(
                $"""
                INSERT INTO "Agents" (
                    "UserId", "AgencyName", "PhoneNumber", "LicenseNumber",
                    "IsVerified", "IsSuspended", "IsRemoved",
                    "RemovedAt", "RemovalReason", "RemovedByAdminId",
                    "CreatedAt", "UpdatedAt")
                SELECT
                    a."UserId",
                    COALESCE(a."AgencyName", ''),
                    COALESCE(a."PhoneNumber", ''),
                    NULL,
                    false,
                    false,
                    true,
                    now(),
                    {Literal(Reason)},
                    COALESCE(
                        (SELECT au."Id" FROM "AspNetUsers" au WHERE au."Id" = a."ReviewedByAdminId"),
                        (SELECT MIN(au2."Id")
                           FROM "AspNetUsers" au2
                           JOIN "AspNetUserRoles" ur ON ur."UserId" = au2."Id"
                           JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                          WHERE r."Name" = 'Admin')),
                    COALESCE(a."CreatedAt", now()),
                    now()
                FROM "AgentApplications" a
                WHERE a."Status" = 'Approved'
                  AND NOT EXISTS (SELECT 1 FROM "Agents" ag WHERE ag."UserId" = a."UserId");
                """);

            // The application is revoked in the same migration, keyed off the marker the insert
            // just wrote, so the two writes cannot disagree about which rows were repaired.
            migrationBuilder.Sql(
                $"""
                UPDATE "AgentApplications" a
                   SET "Status"           = 'Revoked',
                       "RevocationReason" = {Literal(Reason)},
                       "RevokedAt"        = now(),
                       "RevokedByAdminId" = COALESCE(
                           (SELECT au."Id" FROM "AspNetUsers" au WHERE au."Id" = a."ReviewedByAdminId"),
                           (SELECT MIN(au2."Id")
                              FROM "AspNetUsers" au2
                              JOIN "AspNetUserRoles" ur ON ur."UserId" = au2."Id"
                              JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                             WHERE r."Name" = 'Admin')),
                       "UpdatedAt"        = now()
                  FROM "Agents" ag
                 WHERE ag."UserId" = a."UserId"
                   AND ag."IsRemoved" = true
                   AND ag."LicenseNumber" IS NULL
                   AND ag."RemovalReason" LIKE {Literal(MarkerPrefix)}
                   AND a."Status" = 'Approved';
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Reverts only the rows this migration itself created, recognised by the same marker. A
        /// real registration, or anything written after this migration ran, is left alone.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                DELETE FROM "Agents" ag
                 USING "AgentApplications" a
                 WHERE a."UserId" = ag."UserId"
                   AND ag."IsRemoved" = true
                   AND ag."LicenseNumber" IS NULL
                   AND ag."RemovalReason" LIKE {Literal(MarkerPrefix)}
                   AND a."Status" = 'Revoked'
                   AND a."RevocationReason" LIKE {Literal(MarkerPrefix)};
                """);

            migrationBuilder.Sql(
                $"""
                UPDATE "AgentApplications"
                   SET "Status"           = 'Approved',
                       "RevocationReason" = NULL,
                       "RevokedAt"        = NULL,
                       "RevokedByAdminId" = NULL
                 WHERE "Status" = 'Revoked'
                   AND "RevocationReason" LIKE {Literal(MarkerPrefix)};
                """);
        }
    }
}
