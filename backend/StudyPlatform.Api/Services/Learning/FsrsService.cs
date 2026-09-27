using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.Models;
using FsrsSharp.Configuration;
using FsrsSharp.Core;
using FsrsSharp.Models;
using LibraryRating = FsrsSharp.Models.Rating;
using LibraryState = FsrsSharp.Models.State;
using DomainRating = StudyPlatform.Api.Domain.Enums.FsrsRating;

namespace StudyPlatform.Api.Services.Learning;

public sealed record FsrsScheduleResult(
    string State,
    double Difficulty,
    double Stability,
    DateTime DueAtUtc,
    DateTime LastReviewAtUtc,
    double ElapsedDays,
    double ScheduledDays,
    int Repetitions,
    int Lapses);

public sealed class FsrsService
{
    private readonly Scheduler scheduler = new(new FsrsConfig { EnableFuzzing = false });

    public FsrsScheduleResult Schedule(StudentConceptState? current, DomainRating rating, DateTime reviewedAtUtc)
    {
        if (reviewedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Review time must be UTC.", nameof(reviewedAtUtc));

        var beforeReview = current?.LastReviewAtUtc;
        var card = ToCard(current);
        var result = scheduler.ReviewCard(card, ToLibraryRating(rating), new DateTimeOffset(reviewedAtUtc));
        var updated = result.Card;
        var due = updated.Due.UtcDateTime;
        var lastReview = updated.LastReview?.UtcDateTime ?? reviewedAtUtc;
        var elapsedDays = beforeReview is null ? 0 : Math.Max(0, (reviewedAtUtc - beforeReview.Value).TotalDays);
        var scheduledDays = Math.Max(0, (due - lastReview).TotalDays);
        var lapses = (current?.Lapses ?? 0) + (rating == DomainRating.Again && current?.FsrsState?.StartsWith("Review", StringComparison.Ordinal) == true ? 1 : 0);

        return new FsrsScheduleResult(
            $"{updated.State}:{updated.Step?.ToString() ?? "-"}",
            updated.Difficulty ?? 0,
            updated.Stability ?? 0,
            due,
            lastReview,
            elapsedDays,
            scheduledDays,
            (current?.Repetitions ?? 0) + 1,
            lapses);
    }

    private static Card ToCard(StudentConceptState? current)
    {
        if (current is null || string.IsNullOrWhiteSpace(current.FsrsState))
            return new Card(state: LibraryState.New);

        var parts = current.FsrsState.Split(':', 2);
        if (!Enum.TryParse<LibraryState>(parts[0], true, out var state))
            throw new InvalidOperationException("The persisted FSRS state is invalid.");
        int? step = parts.Length == 2 && int.TryParse(parts[1], out var parsedStep) ? parsedStep : null;
        return new Card(
            state: state,
            step: step,
            stability: current.Stability,
            difficulty: current.Difficulty,
            due: current.DueAtUtc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(current.DueAtUtc.Value, DateTimeKind.Utc)),
            lastReview: current.LastReviewAtUtc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(current.LastReviewAtUtc.Value, DateTimeKind.Utc)));
    }

    private static LibraryRating ToLibraryRating(DomainRating rating) => rating switch
    {
        DomainRating.Again => LibraryRating.Again,
        DomainRating.Hard => LibraryRating.Hard,
        DomainRating.Good => LibraryRating.Good,
        DomainRating.Easy => LibraryRating.Easy,
        _ => throw new ArgumentOutOfRangeException(nameof(rating)),
    };
}
