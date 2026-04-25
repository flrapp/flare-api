using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTypedServeValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "type",
                table: "feature_flags",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "serve_boolean_value",
                table: "targeting_rules",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "serve_string_value",
                table: "targeting_rules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "serve_number_value",
                table: "targeting_rules",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "serve_json_value",
                table: "targeting_rules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "default_string_value",
                table: "feature_flag_values",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "default_number_value",
                table: "feature_flag_values",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "default_json_value",
                table: "feature_flag_values",
                type: "text",
                nullable: true);

            // Backfill typed column from legacy bool before dropping it.
            // This is the only data-touching step; the DropColumn below depends on it.
            migrationBuilder.Sql("UPDATE targeting_rules SET serve_boolean_value = serve_value;");

            // Safe to drop now: data preserved in serve_boolean_value, and feature_flags.type
            // defaults to 0 (Boolean) so the read path uses serve_boolean_value for all legacy rows.
            migrationBuilder.DropColumn(
                name: "serve_value",
                table: "targeting_rules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "serve_value",
                table: "targeting_rules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Restore legacy bool column from typed backfill.
            migrationBuilder.Sql("UPDATE targeting_rules SET serve_value = COALESCE(serve_boolean_value, false);");

            migrationBuilder.DropColumn(
                name: "serve_boolean_value",
                table: "targeting_rules");

            migrationBuilder.DropColumn(
                name: "serve_string_value",
                table: "targeting_rules");

            migrationBuilder.DropColumn(
                name: "serve_number_value",
                table: "targeting_rules");

            migrationBuilder.DropColumn(
                name: "serve_json_value",
                table: "targeting_rules");

            migrationBuilder.DropColumn(
                name: "default_string_value",
                table: "feature_flag_values");

            migrationBuilder.DropColumn(
                name: "default_number_value",
                table: "feature_flag_values");

            migrationBuilder.DropColumn(
                name: "default_json_value",
                table: "feature_flag_values");

            migrationBuilder.DropColumn(
                name: "type",
                table: "feature_flags");
        }
    }
}
