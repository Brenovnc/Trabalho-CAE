using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyPlatform.Api.Migrations;

[DbContext(typeof(StudyPlatform.Api.Data.ApplicationDbContext))]
[Migration("20260927170000_AddStudentLastAccessAtUtc")]
public sealed class AddStudentLastAccessAtUtc : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<DateTime>(
            name: "LastAccessAtUtc",
            table: "Students",
            type: "timestamp with time zone",
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "LastAccessAtUtc", table: "Students");
}
