using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommunityDownloadCount",
                table: "Modules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CommunityOriginAuthor",
                table: "Modules",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommunityOriginModuleId",
                table: "Modules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommunitySourceModuleId",
                table: "Modules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCommunityListing",
                table: "Modules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SharedAtUtc",
                table: "Modules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Modules_IsCommunityListing_SharedAtUtc",
                table: "Modules",
                columns: new[] { "IsCommunityListing", "SharedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Modules_IsCommunityListing_SharedAtUtc",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "CommunityDownloadCount",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "CommunityOriginAuthor",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "CommunityOriginModuleId",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "CommunitySourceModuleId",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "IsCommunityListing",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "SharedAtUtc",
                table: "Modules");
        }
    }
}
