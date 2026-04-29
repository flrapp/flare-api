using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveViewPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM project_user_project_permissions
                WHERE permission IN ('ViewSegments', 'ViewTargetingRules');
                """);

            migrationBuilder.Sql("""
                DELETE FROM project_user_scope_permissions
                WHERE permission IN ('ViewSegments', 'ViewTargetingRules');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
