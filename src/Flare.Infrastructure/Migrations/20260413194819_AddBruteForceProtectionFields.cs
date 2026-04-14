using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBruteForceProtectionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_login_attempts",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_brute_force_locked",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_failed_login_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "locked_until",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failed_login_attempts",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_brute_force_locked",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_failed_login_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "locked_until",
                table: "users");
        }
    }
}
