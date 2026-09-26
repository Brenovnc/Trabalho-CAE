using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    EmailNormalized = table.Column<string>(type: "text", nullable: false, computedColumnSql: "lower(\"Email\")", stored: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Classrooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CodeNormalized = table.Column<string>(type: "text", nullable: false, computedColumnSql: "lower(\"Code\")", stored: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classrooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Classrooms_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Modules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modules", x => x.Id);
                    table.CheckConstraint("CK_Modules_Version_Positive", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_Modules_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassroomId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    TemporaryAccessCodeHash = table.Column<string>(type: "text", nullable: true),
                    TemporaryAccessCodeExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TemporaryAccessCodeFailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    IsActivated = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.CheckConstraint("CK_Students_TemporaryAccessCodeFailedAttempts_NonNegative", "\"TemporaryAccessCodeFailedAttempts\" >= 0");
                    table.ForeignKey(
                        name: "FK_Students_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassroomModules",
                columns: table => new
                {
                    ClassroomId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassroomModules", x => new { x.ClassroomId, x.ModuleId });
                    table.ForeignKey(
                        name: "FK_ClassroomModules_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomModules_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Concepts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Definition = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Concepts", x => x.Id);
                    table.UniqueConstraint("AK_Concepts_Id_ModuleId", x => new { x.Id, x.ModuleId });
                    table.ForeignKey(
                        name: "FK_Concepts_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudySessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalActivities = table.Column<int>(type: "integer", nullable: false),
                    CompletedActivities = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudySessions", x => x.Id);
                    table.CheckConstraint("CK_StudySessions_ActivityCounts", "\"TotalActivities\" >= 0 AND \"TotalActivities\" <= 10 AND \"CompletedActivities\" >= 0 AND \"CompletedActivities\" <= \"TotalActivities\"");
                    table.ForeignKey(
                        name: "FK_StudySessions_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudySessions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConceptClues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConceptClues", x => x.Id);
                    table.CheckConstraint("CK_ConceptClues_Position_NonNegative", "\"Position\" >= 0");
                    table.ForeignKey(
                        name: "FK_ConceptClues_Concepts_ConceptId",
                        column: x => x.ConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConceptKeywords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConceptKeywords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConceptKeywords_Concepts_ConceptId",
                        column: x => x.ConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConceptPrerequisites",
                columns: table => new
                {
                    ConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrerequisiteConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConceptPrerequisites", x => new { x.ConceptId, x.PrerequisiteConceptId });
                    table.CheckConstraint("CK_ConceptPrerequisites_NoSelfReference", "\"ConceptId\" <> \"PrerequisiteConceptId\"");
                    table.ForeignKey(
                        name: "FK_ConceptPrerequisites_Concepts_ConceptId_ModuleId",
                        columns: x => new { x.ConceptId, x.ModuleId },
                        principalTable: "Concepts",
                        principalColumns: new[] { "Id", "ModuleId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConceptPrerequisites_Concepts_PrerequisiteConceptId_ModuleId",
                        columns: x => new { x.PrerequisiteConceptId, x.ModuleId },
                        principalTable: "Concepts",
                        principalColumns: new[] { "Id", "ModuleId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FillBlankActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FillBlankActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FillBlankActivities_Concepts_ConceptId",
                        column: x => x.ConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderingActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Instruction = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderingActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderingActivities_Concepts_ConceptId",
                        column: x => x.ConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecognitionActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Statement = table.Column<string>(type: "text", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecognitionActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecognitionActivities_Concepts_ConceptId",
                        column: x => x.ConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentConceptStates",
                columns: table => new
                {
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    LearningState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FreeRecallSuccessCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSuccessAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFailureAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFreeRecallSuccessAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFreeRecallSuccessSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FsrsState = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    Difficulty = table.Column<double>(type: "double precision", nullable: true),
                    Stability = table.Column<double>(type: "double precision", nullable: true),
                    DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastReviewAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ElapsedDays = table.Column<double>(type: "double precision", nullable: true),
                    ScheduledDays = table.Column<double>(type: "double precision", nullable: true),
                    Repetitions = table.Column<int>(type: "integer", nullable: false),
                    Lapses = table.Column<int>(type: "integer", nullable: false),
                    LastFsrsRating = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentConceptStates", x => new { x.StudentId, x.ConceptId });
                    table.CheckConstraint("CK_StudentConceptStates_Difficulty_NonNegative", "\"Difficulty\" IS NULL OR \"Difficulty\" >= 0");
                    table.CheckConstraint("CK_StudentConceptStates_FreeRecallSuccessCount_NonNegative", "\"FreeRecallSuccessCount\" >= 0");
                    table.CheckConstraint("CK_StudentConceptStates_Lapses_NonNegative", "\"Lapses\" >= 0");
                    table.CheckConstraint("CK_StudentConceptStates_Repetitions_NonNegative", "\"Repetitions\" >= 0");
                    table.CheckConstraint("CK_StudentConceptStates_Stability_NonNegative", "\"Stability\" IS NULL OR \"Stability\" >= 0");
                    table.ForeignKey(
                        name: "FK_StudentConceptStates_Concepts_ConceptId",
                        column: x => x.ConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentConceptStates_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentConceptStates_StudySessions_LastFreeRecallSuccessSes~",
                        column: x => x.LastFreeRecallSuccessSessionId,
                        principalTable: "StudySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FillBlankAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FillBlankActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotNumber = table.Column<int>(type: "integer", nullable: false),
                    CorrectText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FillBlankAnswers", x => x.Id);
                    table.CheckConstraint("CK_FillBlankAnswers_Slot_Positive", "\"SlotNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_FillBlankAnswers_FillBlankActivities_FillBlankActivityId",
                        column: x => x.FillBlankActivityId,
                        principalTable: "FillBlankActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FillBlankDistractors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FillBlankActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FillBlankDistractors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FillBlankDistractors_FillBlankActivities_FillBlankActivityId",
                        column: x => x.FillBlankActivityId,
                        principalTable: "FillBlankActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderingActivityItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderingActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderingActivityItems", x => x.Id);
                    table.CheckConstraint("CK_OrderingActivityItems_Position_NonNegative", "\"Position\" >= 0");
                    table.ForeignKey(
                        name: "FK_OrderingActivityItems_OrderingActivities_OrderingActivityId",
                        column: x => x.OrderingActivityId,
                        principalTable: "OrderingActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudySessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RecognitionActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    FillBlankActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderingActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    WasCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnsweredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResponseTimeMs = table.Column<long>(type: "bigint", nullable: true),
                    AttemptsUsed = table.Column<int>(type: "integer", nullable: false),
                    HintsUsed = table.Column<int>(type: "integer", nullable: false),
                    FsrsRating = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    AnswerJson = table.Column<string>(type: "jsonb", nullable: true),
                    ActivitySnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityAttempts", x => x.Id);
                    table.CheckConstraint("CK_ActivityAttempts_ActivityReferenceMatchesType", "(\n    (\"ActivityType\" = 'TrueFalse' AND \"RecognitionActivityId\" IS NOT NULL AND \"FillBlankActivityId\" IS NULL AND \"OrderingActivityId\" IS NULL)\n    OR (\"ActivityType\" = 'FillBlank' AND \"RecognitionActivityId\" IS NULL AND \"FillBlankActivityId\" IS NOT NULL AND \"OrderingActivityId\" IS NULL)\n    OR (\"ActivityType\" = 'Ordering' AND \"RecognitionActivityId\" IS NULL AND \"FillBlankActivityId\" IS NULL AND \"OrderingActivityId\" IS NOT NULL)\n    OR (\"ActivityType\" IN ('Exposure', 'GuessConcept') AND \"RecognitionActivityId\" IS NULL AND \"FillBlankActivityId\" IS NULL AND \"OrderingActivityId\" IS NULL)\n)");
                    table.CheckConstraint("CK_ActivityAttempts_Attempts_Positive", "\"AttemptsUsed\" > 0");
                    table.CheckConstraint("CK_ActivityAttempts_Hints_NonNegative", "\"HintsUsed\" >= 0");
                    table.CheckConstraint("CK_ActivityAttempts_ResponseTime_NonNegative", "\"ResponseTimeMs\" IS NULL OR \"ResponseTimeMs\" >= 0");
                    table.ForeignKey(
                        name: "FK_ActivityAttempts_Concepts_ConceptId",
                        column: x => x.ConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityAttempts_FillBlankActivities_FillBlankActivityId",
                        column: x => x.FillBlankActivityId,
                        principalTable: "FillBlankActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityAttempts_OrderingActivities_OrderingActivityId",
                        column: x => x.OrderingActivityId,
                        principalTable: "OrderingActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityAttempts_RecognitionActivities_RecognitionActivityId",
                        column: x => x.RecognitionActivityId,
                        principalTable: "RecognitionActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityAttempts_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityAttempts_StudySessions_StudySessionId",
                        column: x => x.StudySessionId,
                        principalTable: "StudySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityAttempts_ConceptId",
                table: "ActivityAttempts",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityAttempts_FillBlankActivityId",
                table: "ActivityAttempts",
                column: "FillBlankActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityAttempts_OrderingActivityId",
                table: "ActivityAttempts",
                column: "OrderingActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityAttempts_RecognitionActivityId",
                table: "ActivityAttempts",
                column: "RecognitionActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityAttempts_StudentId_ConceptId_CreatedAtUtc",
                table: "ActivityAttempts",
                columns: new[] { "StudentId", "ConceptId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityAttempts_StudySessionId_CreatedAtUtc",
                table: "ActivityAttempts",
                columns: new[] { "StudySessionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomModules_ModuleId",
                table: "ClassroomModules",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_CodeNormalized",
                table: "Classrooms",
                column: "CodeNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_TeacherId",
                table: "Classrooms",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_ConceptClues_ConceptId_Position",
                table: "ConceptClues",
                columns: new[] { "ConceptId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConceptKeywords_ConceptId_Value",
                table: "ConceptKeywords",
                columns: new[] { "ConceptId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConceptPrerequisites_ConceptId_ModuleId",
                table: "ConceptPrerequisites",
                columns: new[] { "ConceptId", "ModuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConceptPrerequisites_PrerequisiteConceptId_ModuleId",
                table: "ConceptPrerequisites",
                columns: new[] { "PrerequisiteConceptId", "ModuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Concepts_ModuleId",
                table: "Concepts",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Concepts_ModuleId_ExternalId",
                table: "Concepts",
                columns: new[] { "ModuleId", "ExternalId" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FillBlankActivities_ConceptId",
                table: "FillBlankActivities",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_FillBlankAnswers_FillBlankActivityId_SlotNumber",
                table: "FillBlankAnswers",
                columns: new[] { "FillBlankActivityId", "SlotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FillBlankDistractors_FillBlankActivityId_Text",
                table: "FillBlankDistractors",
                columns: new[] { "FillBlankActivityId", "Text" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Modules_TeacherId",
                table: "Modules",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderingActivities_ConceptId",
                table: "OrderingActivities",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderingActivityItems_OrderingActivityId_Position",
                table: "OrderingActivityItems",
                columns: new[] { "OrderingActivityId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecognitionActivities_ConceptId",
                table: "RecognitionActivities",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentConceptStates_ConceptId",
                table: "StudentConceptStates",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentConceptStates_DueAtUtc",
                table: "StudentConceptStates",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StudentConceptStates_LastFreeRecallSuccessSessionId",
                table: "StudentConceptStates",
                column: "LastFreeRecallSuccessSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ClassroomId_EnrollmentNumber",
                table: "Students",
                columns: new[] { "ClassroomId", "EnrollmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_ModuleId",
                table: "StudySessions",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_StudentId_ModuleId_StartedAtUtc",
                table: "StudySessions",
                columns: new[] { "StudentId", "ModuleId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_EmailNormalized",
                table: "Teachers",
                column: "EmailNormalized",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityAttempts");

            migrationBuilder.DropTable(
                name: "ClassroomModules");

            migrationBuilder.DropTable(
                name: "ConceptClues");

            migrationBuilder.DropTable(
                name: "ConceptKeywords");

            migrationBuilder.DropTable(
                name: "ConceptPrerequisites");

            migrationBuilder.DropTable(
                name: "FillBlankAnswers");

            migrationBuilder.DropTable(
                name: "FillBlankDistractors");

            migrationBuilder.DropTable(
                name: "OrderingActivityItems");

            migrationBuilder.DropTable(
                name: "StudentConceptStates");

            migrationBuilder.DropTable(
                name: "RecognitionActivities");

            migrationBuilder.DropTable(
                name: "FillBlankActivities");

            migrationBuilder.DropTable(
                name: "OrderingActivities");

            migrationBuilder.DropTable(
                name: "StudySessions");

            migrationBuilder.DropTable(
                name: "Concepts");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "Modules");

            migrationBuilder.DropTable(
                name: "Classrooms");

            migrationBuilder.DropTable(
                name: "Teachers");
        }
    }
}
