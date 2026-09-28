namespace StudyPlatform.Api.Domain.Enums;

public enum ModuleStatus
{
    Draft,
    Published,
    Archived,
}

public enum ClassroomStatus
{
    Active,
    Archived,
}

public enum LearningState
{
    New,
    Exposure,
    Recognition,
    GuidedRecall,
    FreeRecall,
    Mastered,
}

public enum StudySessionStatus
{
    Active,
    Completed,
    Abandoned,
}

public enum StudySessionMode
{
    Normal,
    FreePractice,
}

public enum ActivityType
{
    Exposure,
    TrueFalse,
    FillBlank,
    GuessConcept,
    Ordering,
}

public enum FsrsRating
{
    Again,
    Hard,
    Good,
    Easy,
}
