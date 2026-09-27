using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Services.Learning;
using Xunit;

namespace StudyPlatform.Tests;

public sealed class LearningEngineUnitTests
{
    private readonly FsrsService fsrs = new();
    private readonly PerformanceRatingService performance = new();
    private readonly ActivityCompatibilityService activities = new();
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(LearningState.Exposure, ActivityType.Exposure)]
    [InlineData(LearningState.Recognition, ActivityType.TrueFalse)]
    [InlineData(LearningState.Recognition, ActivityType.Ordering)]
    [InlineData(LearningState.GuidedRecall, ActivityType.FillBlank)]
    [InlineData(LearningState.GuidedRecall, ActivityType.Ordering)]
    [InlineData(LearningState.FreeRecall, ActivityType.GuessConcept)]
    [InlineData(LearningState.Mastered, ActivityType.GuessConcept)]
    public void ActivityTypesFollowPedagogicalCompatibility(LearningState state, ActivityType expected) =>
        Assert.Contains(expected, activities.GetCompatibleTypes(state));

    [Fact]
    public void NewConceptDoesNotHaveAnFsrsDueDateUntilReviewed()
    {
        var state = new StudentConceptState();
        Assert.Null(state.DueAtUtc);
        Assert.Null(state.FsrsState);
    }

    [Theory]
    [InlineData(false, 1, 0, 1000, FsrsRating.Again)]
    [InlineData(true, 2, 0, 1000, FsrsRating.Hard)]
    [InlineData(true, 1, 2, 1000, FsrsRating.Hard)]
    [InlineData(true, 1, 0, 5000, FsrsRating.Easy)]
    [InlineData(true, 1, 0, 5001, FsrsRating.Good)]
    [InlineData(true, 1, 1, 1000, FsrsRating.Good)]
    public void PerformanceRatingIsDeterministic(bool correct, int attempts, int hints, long elapsed, FsrsRating expected) =>
        Assert.Equal(expected, performance.Infer(correct, attempts, hints, elapsed));

    [Theory]
    [InlineData(FsrsRating.Again)]
    [InlineData(FsrsRating.Hard)]
    [InlineData(FsrsRating.Good)]
    [InlineData(FsrsRating.Easy)]
    public void FsrsSchedulesAllRatingsAndReturnsPersistableValues(FsrsRating rating)
    {
        var result = fsrs.Schedule(new StudentConceptState(), rating, Now);
        Assert.Equal(Now, result.LastReviewAtUtc);
        Assert.True(result.DueAtUtc > Now);
        Assert.True(result.Difficulty >= 1);
        Assert.True(result.Stability > 0);
        Assert.Equal(1, result.Repetitions);
        Assert.True(result.State.StartsWith("Learning:", StringComparison.Ordinal) || result.State == "Review:-");
        Assert.InRange(result.State.Length, 1, 24);
    }

    [Fact]
    public void AgainInReviewIncreasesLapsesAndRepeatsIncrease()
    {
        var first = fsrs.Schedule(new StudentConceptState(), FsrsRating.Good, Now);
        var reviewState = StateFrom(first);
        // Move through the default short learning steps before testing a lapse in Review.
        reviewState = StateFrom(fsrs.Schedule(reviewState, FsrsRating.Good, Now.AddMinutes(2)));
        reviewState = StateFrom(fsrs.Schedule(reviewState, FsrsRating.Good, Now.AddDays(2)));
        var lapsed = fsrs.Schedule(reviewState, FsrsRating.Again, Now.AddDays(3));
        Assert.Equal(reviewState.Repetitions + 1, lapsed.Repetitions);
        Assert.Equal(reviewState.Lapses + 1, lapsed.Lapses);
        Assert.True(lapsed.DueAtUtc > Now.AddDays(3));
    }

    [Fact]
    public void FsrsCarriesElapsedAndScheduledIntervalsForward()
    {
        var first = fsrs.Schedule(new StudentConceptState(), FsrsRating.Good, Now);
        var second = fsrs.Schedule(StateFrom(first), FsrsRating.Good, Now.AddDays(1));
        Assert.Equal(1, second.ElapsedDays);
        Assert.True(second.ScheduledDays > 0);
        Assert.Equal(2, second.Repetitions);
    }

    private static StudentConceptState StateFrom(FsrsScheduleResult result) => new()
    {
        FsrsState = result.State,
        Difficulty = result.Difficulty,
        Stability = result.Stability,
        DueAtUtc = result.DueAtUtc,
        LastReviewAtUtc = result.LastReviewAtUtc,
        ElapsedDays = result.ElapsedDays,
        ScheduledDays = result.ScheduledDays,
        Repetitions = result.Repetitions,
        Lapses = result.Lapses,
    };
}
