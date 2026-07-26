using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vms.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillIdentitySecurityStamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET
                    "SecurityStamp" = COALESCE(
                        "SecurityStamp",
                        md5(random()::text || clock_timestamp()::text || "Id"::text)),
                    "ConcurrencyStamp" = COALESCE(
                        "ConcurrencyStamp",
                        md5(random()::text || clock_timestamp()::text || "Id"::text)),
                    "LockoutEnabled" = TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET
                    "SecurityStamp" = NULL,
                    "ConcurrencyStamp" = NULL,
                    "LockoutEnabled" = FALSE;
                """);
        }
    }
}
