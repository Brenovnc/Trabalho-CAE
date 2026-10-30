using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCrosswordAndAdvanceReviewSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowAdvanceReview",
                table: "Modules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CrosswordAllowAllWords",
                table: "Modules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CrosswordEnabled",
                table: "Modules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CrosswordClue",
                table: "Concepts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CrosswordWord",
                table: "Concepts",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowAdvanceReview",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "CrosswordAllowAllWords",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "CrosswordEnabled",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "CrosswordClue",
                table: "Concepts");

            migrationBuilder.DropColumn(
                name: "CrosswordWord",
                table: "Concepts");
        }
    }
}
