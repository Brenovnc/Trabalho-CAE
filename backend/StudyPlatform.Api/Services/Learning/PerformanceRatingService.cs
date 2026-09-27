namespace StudyPlatform.Api.Services.Learning;

public sealed class PerformanceRatingService
{
    public Domain.Enums.FsrsRating Infer(bool wasCorrect, int attemptsUsed, int hintsUsed, long responseTimeMs)
    {
        if (attemptsUsed < 1) throw new ArgumentOutOfRangeException(nameof(attemptsUsed));
        if (hintsUsed < 0) throw new ArgumentOutOfRangeException(nameof(hintsUsed));
        if (responseTimeMs < 0) throw new ArgumentOutOfRangeException(nameof(responseTimeMs));
        if (!wasCorrect) return Domain.Enums.FsrsRating.Again;
        if (attemptsUsed > 1 || hintsUsed >= 2) return Domain.Enums.FsrsRating.Hard;
        if (attemptsUsed == 1 && hintsUsed == 0 && responseTimeMs <= 5_000)
            return Domain.Enums.FsrsRating.Easy;
        return Domain.Enums.FsrsRating.Good;
    }
}
