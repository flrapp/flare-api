using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInitialUserProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "initial_user",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "initial_user",
                table: "users");
        }
    }
}
