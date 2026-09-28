using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Learning;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Services.Learning;

namespace StudyPlatform.Api.Services.Learning;

internal sealed record SnapshotKeyword(Guid Id, string Value);
internal sealed record SnapshotClue(Guid Id, string Text);
internal sealed record SnapshotSlot(int SlotNumber, string CorrectText);
internal sealed record SnapshotOption(Guid Id, string Text);
internal sealed record SnapshotOrderItem(Guid Id, int Position, string Text);
internal sealed record ActivitySnapshot(
    int Version,
    string Type,
    Guid ConceptId,
    string ConceptName,
    string? Definition = null,
    IReadOnlyList<SnapshotKeyword>? Keywords = null,
    IReadOnlyList<SnapshotClue>? Clues = null,
    Guid? ActivityId = null,
    string? Statement = null,
    bool? IsCorrect = null,
    string? Explanation = null,
    string? Text = null,
    IReadOnlyList<SnapshotSlot>? Slots = null,
    IReadOnlyList<SnapshotOption>? Options = null,
    string? Instruction = null,
    IReadOnlyList<SnapshotOrderItem>? Items = null);

/// <summary>Incremental, deterministic selection. Priorities: due, recent error (14 days), progress, NEW.</summary>
public sealed class ActivitySelectionService(
    ApplicationDbContext db,
    ActivityCompatibilityService compatibility,
    ConceptEligibilityService eligibility,
    LearningProgressionService progression,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan RecentFailureWindow = TimeSpan.FromDays(14);

    public async Task<SessionActivityPresentation?> PresentNextAsync(StudySession session, CancellationToken ct)
    {
        if (session.Status != StudySessionStatus.Active || session.CompletedActivities >= 10)
            return null;

        var concepts = await db.Concepts.AsNoTracking()
            .Where(c => c.ModuleId == session.ModuleId && c.IsActive)
            .Include(c => c.Keywords.Where(k => k.IsActive))
            .Include(c => c.Clues.Where(cl => cl.IsActive))
            .Include(c => c.RecognitionActivities.Where(a => a.IsActive))
            .Include(c => c.FillBlankActivities.Where(a => a.IsActive)).ThenInclude(a => a.Answers)
            .Include(c => c.FillBlankActivities.Where(a => a.IsActive)).ThenInclude(a => a.Distractors)
            .Include(c => c.OrderingActivities.Where(a => a.IsActive)).ThenInclude(a => a.Items)
            .AsSplitQuery()
            .OrderBy(c => c.Id).ToListAsync(ct);
        var states = await db.StudentConceptStates.AsNoTracking()
            .Where(s => s.StudentId == session.StudentId && concepts.Select(c => c.Id).Contains(s.ConceptId))
            .ToDictionaryAsync(s => s.ConceptId, ct);
        var used = await db.SessionActivityPresentations.AsNoTracking()
            .Where(p => p.StudySessionId == session.Id)
            .Select(p => new { p.ConceptId, p.ActivityType, p.ActivityId })
            .ToListAsync(ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var candidates = new List<Candidate>();

        foreach (var concept in concepts)
        {
            states.TryGetValue(concept.Id, out var state);
            var learningState = state?.LearningState ?? LearningState.New;
            if (learningState == LearningState.Mastered && (state?.DueAtUtc is null || Utc(state.DueAtUtc.Value) > now)) continue;
            if (learningState == LearningState.FreeRecall && state?.FreeRecallSuccessCount > 0 && (state.DueAtUtc is null || Utc(state.DueAtUtc.Value) > now)) continue;
            if (learningState == LearningState.New && !await eligibility.CanIntroduceAsync(session.StudentId, concept.Id, ct)) continue;            var tier = GetTier(learningState, state, now);
            foreach (var type in compatibility.GetCompatibleTypes(learningState == LearningState.New ? LearningState.Exposure : learningState))
            {
                foreach (var choice in BuildActivities(concept, type))
                {
                    if (used.Any(x => x.ConceptId == concept.Id && x.ActivityType == type && x.ActivityId == choice.Id)) continue;
                    candidates.Add(new Candidate(concept, state, learningState, tier, type, choice.Id, choice.Snapshot));
                }
            }
        }

        if (candidates.Count == 0) return null;
        var previousConceptId = await db.SessionActivityPresentations.AsNoTracking()
            .Where(x => x.StudySessionId == session.Id).OrderByDescending(x => x.SequenceNumber)
            .Select(x => (Guid?)x.ConceptId).FirstOrDefaultAsync(ct);
        var ordered = candidates.OrderBy(c => c.Tier)
            .ThenBy(c => c.Tier == 0 ? c.State?.DueAtUtc : null)
            .ThenByDescending(c => c.Tier == 1 ? c.State?.LastFailureAtUtc : null)
            .ThenBy(c => c.Type == PrimaryType(c.State?.LearningState ?? LearningState.New) ? 0 : 1)
            .ThenBy(c => c.Concept.Id)
            .ThenBy(c => c.ActivityId);
        var chosen = ordered.FirstOrDefault(c => c.Tier != candidates.Min(x => x.Tier) || c.Concept.Id != previousConceptId)
            ?? ordered.First();

        if (chosen.LearningState == LearningState.New)
            await progression.MarkPresentedAsync(session.StudentId, chosen.Concept.Id, ct);

        var sequence = await db.SessionActivityPresentations.Where(x => x.StudySessionId == session.Id).CountAsync(ct) + 1;
        var presentation = new SessionActivityPresentation
        {
            StudySessionId = session.Id,
            SequenceNumber = sequence,
            ConceptId = chosen.Concept.Id,
            ActivityType = chosen.Type,
            ActivityId = chosen.ActivityId,
            StartedAtUtc = now,
            RevealedClueCount = chosen.Type == ActivityType.GuessConcept ? 1 : 0,
            SnapshotJson = JsonSerializer.Serialize(chosen.Snapshot, JsonOptions),
        };
        db.SessionActivityPresentations.Add(presentation);
        session.TotalActivities = Math.Min(sequence, 10);
        await db.SaveChangesAsync(ct);
        return presentation;
    }

    public PresentedActivityResponse MapPublic(SessionActivityPresentation presentation)
    {
        var snapshot = JsonSerializer.Deserialize<ActivitySnapshot>(presentation.SnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException("Saved activity snapshot is invalid.");
        object payload = snapshot.Type switch
        {
            "EXPOSURE" => new { keywordCount = snapshot.Keywords?.Count ?? 0,
                revealedCount = presentation.RevealedClueCount,
                revealedKeywords = (snapshot.Keywords ?? []).Take(presentation.RevealedClueCount).Select(x => x.Value).ToArray(),
                segments = BuildExposureSegments(snapshot.Definition ?? string.Empty, snapshot.Keywords ?? [], presentation.RevealedClueCount) },
            "TRUE_FALSE" => new { statement = snapshot.Statement },
            "FILL_BLANK" => new { text = snapshot.Text, slotNumbers = (snapshot.Slots ?? []).Select(x => x.SlotNumber).ToArray(),
                options = (snapshot.Options ?? []).Select(x => x.Text).ToArray() },
            "GUESS_CONCEPT" => new { clueCount = snapshot.Clues?.Count ?? 0,
                clues = (snapshot.Clues ?? []).Take(presentation.RevealedClueCount).Select(x => x.Text).ToArray() },
            "ORDERING" => new { instruction = snapshot.Instruction, items = (snapshot.Items ?? []).Select(x => new { id = x.Id, text = x.Text }).ToArray() },
            _ => throw new InvalidOperationException("Unknown saved activity type."),
        };
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload, JsonOptions));
        return new PresentedActivityResponse(presentation.Id, snapshot.Type, presentation.ConceptId, snapshot.ConceptName, presentation.StartedAtUtc, document.RootElement.Clone());
    }


    private static IReadOnlyList<ExposureSegment> BuildExposureSegments(string definition, IReadOnlyList<SnapshotKeyword> keywords, int revealedCount)
    {
        var matches = new List<(int Start, int End, int KeywordIndex)>();
        for (var keywordIndex = 0; keywordIndex < keywords.Count; keywordIndex++)
        {
            var keyword = keywords[keywordIndex].Value;
            if (string.IsNullOrWhiteSpace(keyword)) continue;
            var searchFrom = 0;
            while (searchFrom < definition.Length)
            {
                var start = definition.IndexOf(keyword, searchFrom, StringComparison.OrdinalIgnoreCase);
                if (start < 0) break;
                matches.Add((start, start + keyword.Length, keywordIndex));
                searchFrom = start + keyword.Length;
            }
        }

        matches = matches.OrderBy(match => match.Start)
            .ThenByDescending(match => match.End - match.Start)
            .ThenBy(match => match.KeywordIndex)
            .ToList();
        var segments = new List<ExposureSegment>();
        var representedKeywords = new HashSet<int>();
        var cursor = 0;
        foreach (var match in matches)
        {
            if (match.Start < cursor) continue;
            if (match.Start > cursor) segments.Add(new ExposureSegment(definition[cursor..match.Start], null));
            representedKeywords.Add(match.KeywordIndex);
            segments.Add(new ExposureSegment(
                match.KeywordIndex < revealedCount ? keywords[match.KeywordIndex].Value : null,
                match.KeywordIndex));
            cursor = match.End;
        }
        if (cursor < definition.Length) segments.Add(new ExposureSegment(definition[cursor..], null));

        for (var index = 0; index < keywords.Count; index++)
        {
            if (representedKeywords.Contains(index)) continue;
            segments.Add(new ExposureSegment(index < revealedCount ? keywords[index].Value : null, index));
        }
        return segments;
    }

    private sealed record ExposureSegment(string? Text, int? KeywordIndex);
    internal ActivitySnapshot ReadSnapshot(SessionActivityPresentation presentation) =>
        JsonSerializer.Deserialize<ActivitySnapshot>(presentation.SnapshotJson, JsonOptions)
        ?? throw new InvalidOperationException("Saved activity snapshot is invalid.");

    private static int GetTier(LearningState learningState, StudentConceptState? state, DateTime now)
    {
        if (state?.DueAtUtc is not null && Utc(state.DueAtUtc.Value) <= now) return 0;
        if (state?.LastFailureAtUtc is not null && Utc(state.LastFailureAtUtc.Value) >= now - RecentFailureWindow) return 1;
        return learningState == LearningState.New ? 3 : 2;
    }

    private static ActivityType PrimaryType(LearningState state) => state switch
    {
        LearningState.Exposure => ActivityType.Exposure,
        LearningState.Recognition => ActivityType.TrueFalse,
        LearningState.GuidedRecall => ActivityType.FillBlank,
        LearningState.FreeRecall or LearningState.Mastered => ActivityType.GuessConcept,
        _ => ActivityType.Exposure,
    };

    private static IEnumerable<ActivityChoice> BuildActivities(Concept concept, ActivityType type)
    {
        switch (type)
        {
            case ActivityType.Exposure:
                if (concept.Keywords.Count > 0)
                    yield return new(null, new ActivitySnapshot(1, "EXPOSURE", concept.Id, concept.Name, concept.Definition,
                        concept.Keywords.OrderBy(x => x.Id).Select(x => new SnapshotKeyword(x.Id, x.Value)).ToArray()));
                break;
            case ActivityType.TrueFalse:
                foreach (var a in concept.RecognitionActivities.OrderBy(x => x.Id))
                    yield return new(a.Id, new ActivitySnapshot(1, "TRUE_FALSE", concept.Id, concept.Name, ActivityId: a.Id,
                        Statement: a.Statement, IsCorrect: a.IsCorrect, Explanation: a.Explanation));
                break;
            case ActivityType.FillBlank:
                foreach (var a in concept.FillBlankActivities.OrderBy(x => x.Id))
                {
                    var options = a.Answers.Select(x => new SnapshotOption(x.Id, x.CorrectText))
                        .Concat(a.Distractors.Select(x => new SnapshotOption(x.Id, x.Text)))
                        .OrderBy(_ => Guid.NewGuid()).ToArray();
                    yield return new(a.Id, new ActivitySnapshot(1, "FILL_BLANK", concept.Id, concept.Name, ActivityId: a.Id,
                        Text: a.Text, Slots: a.Answers.OrderBy(x => x.SlotNumber).Select(x => new SnapshotSlot(x.SlotNumber, x.CorrectText)).ToArray(), Options: options));
                }
                break;
            case ActivityType.GuessConcept:
                if (concept.Clues.Count > 0)
                    yield return new(null, new ActivitySnapshot(1, "GUESS_CONCEPT", concept.Id, concept.Name,
                        Clues: concept.Clues.OrderBy(x => x.Position).Select(x => new SnapshotClue(x.Id, x.Text)).ToArray()));
                break;
            case ActivityType.Ordering:
                foreach (var a in concept.OrderingActivities.OrderBy(x => x.Id))
                    yield return new(a.Id, new ActivitySnapshot(1, "ORDERING", concept.Id, concept.Name, ActivityId: a.Id,
                        Instruction: a.Instruction, Items: a.Items.OrderBy(x => x.Position).Select(x => new SnapshotOrderItem(x.Id, x.Position, x.Text)).OrderBy(_ => Guid.NewGuid()).ToArray()));
                break;
        }
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private sealed record ActivityChoice(Guid? Id, ActivitySnapshot Snapshot);
    private sealed record Candidate(Concept Concept, StudentConceptState? State, LearningState LearningState, int Tier, ActivityType Type, Guid? ActivityId, ActivitySnapshot Snapshot);
}
