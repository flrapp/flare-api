using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropTargetingRulesPriorityUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_targeting_rules_feature_flag_value_id_priority",
                table: "targeting_rules");

            migrationBuilder.CreateIndex(
                name: "ix_targeting_rules_feature_flag_value_id_priority",
                table: "targeting_rules",
                columns: new[] { "feature_flag_value_id", "priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_targeting_rules_feature_flag_value_id_priority",
                table: "targeting_rules");

            migrationBuilder.CreateIndex(
                name: "ix_targeting_rules_feature_flag_value_id_priority",
                table: "targeting_rules",
                columns: new[] { "feature_flag_value_id", "priority" },
                unique: true);
        }
    }
}
