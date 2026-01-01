using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    global_role = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alias = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    api_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                    table.ForeignKey(
                        name: "fk_projects_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "feature_flags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feature_flags", x => x.id);
                    table.ForeignKey(
                        name: "fk_feature_flags_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_by = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_invitations_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_invitations_users_invited_by",
                        column: x => x.invited_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_by = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_users_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_project_users_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scopes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alias = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scopes", x => x.id);
                    table.ForeignKey(
                        name: "fk_scopes_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_user_project_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_user_project_permissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_user_project_permissions_project_users_project_user",
                        column: x => x.project_user_id,
                        principalTable: "project_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feature_flag_values",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_flag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feature_flag_values", x => x.id);
                    table.ForeignKey(
                        name: "fk_feature_flag_values_feature_flags_feature_flag_id",
                        column: x => x.feature_flag_id,
                        principalTable: "feature_flags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_feature_flag_values_scopes_scope_id",
                        column: x => x.scope_id,
                        principalTable: "scopes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_user_scope_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_user_scope_permissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_user_scope_permissions_project_users_project_user_id",
                        column: x => x.project_user_id,
                        principalTable: "project_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_project_user_scope_permissions_scopes_scope_id",
                        column: x => x.scope_id,
                        principalTable: "scopes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_feature_flag_values_feature_flag_id_scope_id",
                table: "feature_flag_values",
                columns: new[] { "feature_flag_id", "scope_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_feature_flag_values_scope_id",
                table: "feature_flag_values",
                column: "scope_id");

            migrationBuilder.CreateIndex(
                name: "ix_feature_flags_project_id_key",
                table: "feature_flags",
                columns: new[] { "project_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invitations_invited_by",
                table: "invitations",
                column: "invited_by");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_project_id",
                table: "invitations",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_token",
                table: "invitations",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_user_project_permissions_project_user_id_permission",
                table: "project_user_project_permissions",
                columns: new[] { "project_user_id", "permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_user_scope_permissions_project_user_id_scope_id_per",
                table: "project_user_scope_permissions",
                columns: new[] { "project_user_id", "scope_id", "permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_user_scope_permissions_scope_id",
                table: "project_user_scope_permissions",
                column: "scope_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_users_project_id_user_id",
                table: "project_users",
                columns: new[] { "project_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_users_user_id",
                table: "project_users",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_projects_alias",
                table: "projects",
                column: "alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_api_key",
                table: "projects",
                column: "api_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_created_by",
                table: "projects",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_scopes_project_id_alias",
                table: "scopes",
                columns: new[] { "project_id", "alias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feature_flag_values");

            migrationBuilder.DropTable(
                name: "invitations");

            migrationBuilder.DropTable(
                name: "project_user_project_permissions");

            migrationBuilder.DropTable(
                name: "project_user_scope_permissions");

            migrationBuilder.DropTable(
                name: "feature_flags");

            migrationBuilder.DropTable(
                name: "project_users");

            migrationBuilder.DropTable(
                name: "scopes");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
