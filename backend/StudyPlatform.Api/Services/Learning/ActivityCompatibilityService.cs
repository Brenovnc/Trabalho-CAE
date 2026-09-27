using StudyPlatform.Api.Domain.Enums;

namespace StudyPlatform.Api.Services.Learning;

public sealed class ActivityCompatibilityService
{
    public IReadOnlyList<ActivityType> GetCompatibleTypes(LearningState state) => state switch
    {
        LearningState.Exposure => [ActivityType.Exposure],
        LearningState.Recognition => [ActivityType.TrueFalse, ActivityType.Ordering],
        LearningState.GuidedRecall => [ActivityType.FillBlank, ActivityType.Ordering],
        LearningState.FreeRecall or LearningState.Mastered => [ActivityType.GuessConcept],
        _ => [],
    };
}
