using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace StudyPlatform.Api.Migrations;

[DbContext(typeof(StudyPlatform.Api.Data.ApplicationDbContext))]
[Migration("20260927120000_SessionActivityPresentations")]
public partial class SessionActivityPresentations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "PresentationId",
            table: "ActivityAttempts",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "SessionActivityPresentations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                StudySessionId = table.Column<Guid>(type: "uuid", nullable: false),
                SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                ConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                ActivityType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                ActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                AnsweredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                RevealedClueCount = table.Column<int>(type: "integer", nullable: false),
                SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SessionActivityPresentations", x => x.Id);
                table.CheckConstraint("CK_SessionActivityPresentations_Sequence_Positive", "\"SequenceNumber\" > 0");
                table.CheckConstraint("CK_SessionActivityPresentations_RevealedClues_NonNegative", "\"RevealedClueCount\" >= 0");
                table.ForeignKey("FK_SessionActivityPresentations_Concepts_ConceptId", x => x.ConceptId, "Concepts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_SessionActivityPresentations_StudySessions_StudySessionId", x => x.StudySessionId, "StudySessions", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_SessionActivityPresentations_ConceptId", "SessionActivityPresentations", "ConceptId");
        migrationBuilder.CreateIndex("IX_SessionActivityPresentations_StudySessionId_AnsweredAtUtc", "SessionActivityPresentations", new[] { "StudySessionId", "AnsweredAtUtc" });
        migrationBuilder.CreateIndex("IX_SessionActivityPresentations_StudySessionId_SequenceNumber", "SessionActivityPresentations", new[] { "StudySessionId", "SequenceNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_ActivityAttempts_PresentationId", "ActivityAttempts", "PresentationId", unique: true);
        migrationBuilder.AddForeignKey("FK_ActivityAttempts_SessionActivityPresentations_PresentationId", "ActivityAttempts", "PresentationId", "SessionActivityPresentations", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_ActivityAttempts_SessionActivityPresentations_PresentationId", "ActivityAttempts");
        migrationBuilder.DropTable("SessionActivityPresentations");
        migrationBuilder.DropIndex("IX_ActivityAttempts_PresentationId", "ActivityAttempts");
        migrationBuilder.DropColumn("PresentationId", "ActivityAttempts");
    }
}
