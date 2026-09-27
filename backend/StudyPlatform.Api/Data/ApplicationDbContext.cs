using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Models.Activities;

namespace StudyPlatform.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<ClassroomModule> ClassroomModules => Set<ClassroomModule>();
    public DbSet<Concept> Concepts => Set<Concept>();
    public DbSet<ConceptPrerequisite> ConceptPrerequisites => Set<ConceptPrerequisite>();
    public DbSet<ConceptKeyword> ConceptKeywords => Set<ConceptKeyword>();
    public DbSet<ConceptClue> ConceptClues => Set<ConceptClue>();
    public DbSet<RecognitionActivity> RecognitionActivities => Set<RecognitionActivity>();
    public DbSet<FillBlankActivity> FillBlankActivities => Set<FillBlankActivity>();
    public DbSet<FillBlankAnswer> FillBlankAnswers => Set<FillBlankAnswer>();
    public DbSet<FillBlankDistractor> FillBlankDistractors => Set<FillBlankDistractor>();
    public DbSet<OrderingActivity> OrderingActivities => Set<OrderingActivity>();
    public DbSet<OrderingActivityItem> OrderingActivityItems => Set<OrderingActivityItem>();
    public DbSet<StudentConceptState> StudentConceptStates => Set<StudentConceptState>();
    public DbSet<StudySession> StudySessions => Set<StudySession>();
    public DbSet<ActivityAttempt> ActivityAttempts => Set<ActivityAttempt>();
    public DbSet<SessionActivityPresentation> SessionActivityPresentations => Set<SessionActivityPresentation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // All DateTime values in this model represent UTC instants.
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(entity => entity.GetProperties())
                     .Where(property =>
                         property.ClrType == typeof(DateTime) ||
                         Nullable.GetUnderlyingType(property.ClrType) == typeof(DateTime)))
        {
            property.SetColumnType("timestamp with time zone");
        }

        base.OnModelCreating(modelBuilder);
    }
}