using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "targeting_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_flag_value_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    serve_value = table.Column<bool>(type: "boolean", nullable: false),
                    condition_operator = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_targeting_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_targeting_rules_feature_flag_values_feature_flag_value_id",
                        column: x => x.feature_flag_value_id,
                        principalTable: "feature_flag_values",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "targeting_conditions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    targeting_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attribute_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    @operator = table.Column<string>(name: "operator", type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_targeting_conditions", x => x.id);
                    table.ForeignKey(
                        name: "fk_targeting_conditions_targeting_rules_targeting_rule_id",
                        column: x => x.targeting_rule_id,
                        principalTable: "targeting_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_targeting_conditions_targeting_rule_id",
                table: "targeting_conditions",
                column: "targeting_rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_targeting_rules_feature_flag_value_id_priority",
                table: "targeting_rules",
                columns: new[] { "feature_flag_value_id", "priority" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "targeting_conditions");

            migrationBuilder.DropTable(
                name: "targeting_rules");
        }
    }
}
