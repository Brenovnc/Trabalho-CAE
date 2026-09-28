using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFreePracticeStudySessionMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "StudySessions",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<Guid>(
                name: "SelectedConceptId",
                table: "StudySessions",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mode",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "SelectedConceptId",
                table: "StudySessions");
        }
    }
}
