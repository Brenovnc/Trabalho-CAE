using System.ComponentModel.DataAnnotations;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Models.Activities;

namespace StudyPlatform.Api.DTOs.Concepts;

public sealed class SaveConceptRequest
{
    [Required, StringLength(160, MinimumLength = 1)] public string Name { get; init; } = string.Empty;
    [Required, StringLength(12000, MinimumLength = 1)] public string Definition { get; init; } = string.Empty;
    [Required] public List<string> Keywords { get; init; } = [];
    [Required] public List<string> Clues { get; init; } = [];
    [Required] public List<Guid> PrerequisiteIds { get; init; } = [];
    [Required] public List<RecognitionActivityInput> RecognitionActivities { get; init; } = [];
    [Required] public List<FillBlankActivityInput> FillBlankActivities { get; init; } = [];
    [Required] public List<OrderingActivityInput> OrderingActivities { get; init; } = [];
}

public sealed class RecognitionActivityInput
{
    public Guid? Id { get; init; }
    [Required, StringLength(2000, MinimumLength = 1)] public string Statement { get; init; } = string.Empty;
    public bool IsCorrect { get; init; }
    [Required, StringLength(2000, MinimumLength = 1)] public string Explanation { get; init; } = string.Empty;
}

public sealed class FillBlankActivityInput
{
    public Guid? Id { get; init; }
    [Required, StringLength(4000, MinimumLength = 1)] public string Text { get; init; } = string.Empty;
    [Required] public List<FillBlankAnswerInput> Answers { get; init; } = [];
    [Required] public List<string> Distractors { get; init; } = [];
}

public sealed class FillBlankAnswerInput
{
    public int SlotNumber { get; init; }
    [Required, StringLength(300, MinimumLength = 1)] public string CorrectText { get; init; } = string.Empty;
}

public sealed class OrderingActivityInput
{
    public Guid? Id { get; init; }
    [Required, StringLength(1000, MinimumLength = 1)] public string Instruction { get; init; } = string.Empty;
    [Required] public List<string> Items { get; init; } = [];
}

public sealed record ConceptSummaryResponse(Guid Id, string Name, bool IsActive, int PrerequisiteCount);
public sealed record ConceptDetailsResponse(
    Guid Id, Guid ModuleId, string Name, string Definition, bool IsActive,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    IReadOnlyList<string> Keywords, IReadOnlyList<string> Clues,
    IReadOnlyList<Guid> PrerequisiteIds,
    IReadOnlyList<RecognitionActivityResponse> RecognitionActivities,
    IReadOnlyList<FillBlankActivityResponse> FillBlankActivities,
    IReadOnlyList<OrderingActivityResponse> OrderingActivities);
public sealed record RecognitionActivityResponse(Guid Id, string Statement, bool IsCorrect, string Explanation, bool IsActive);
public sealed record FillBlankAnswerResponse(int SlotNumber, string CorrectText);
public sealed record FillBlankActivityResponse(Guid Id, string Text, bool IsActive,
    IReadOnlyList<FillBlankAnswerResponse> Answers, IReadOnlyList<string> Distractors);
public sealed record OrderingActivityResponse(Guid Id, string Instruction, bool IsActive,
    IReadOnlyList<string> Items);

public static class ConceptResponseMapper
{
    public static ConceptDetailsResponse Map(Concept concept) => new(
        concept.Id, concept.ModuleId, concept.Name, concept.Definition, concept.IsActive,
        concept.CreatedAtUtc, concept.UpdatedAtUtc,
        concept.Keywords.Where(item => item.IsActive).OrderBy(item => item.Value).Select(item => item.Value).ToArray(),
        concept.Clues.Where(item => item.IsActive).OrderBy(item => item.Position).Select(item => item.Text).ToArray(),
        concept.Prerequisites.Select(item => item.PrerequisiteConceptId).ToArray(),
        concept.RecognitionActivities.OrderBy(item => item.CreatedAtUtc)
            .Select(item => new RecognitionActivityResponse(item.Id, item.Statement, item.IsCorrect, item.Explanation, item.IsActive)).ToArray(),
        concept.FillBlankActivities.OrderBy(item => item.CreatedAtUtc)
            .Select(item => new FillBlankActivityResponse(item.Id, item.Text, item.IsActive,
                item.Answers.OrderBy(answer => answer.SlotNumber).Select(answer => new FillBlankAnswerResponse(answer.SlotNumber, answer.CorrectText)).ToArray(),
                item.Distractors.Select(distractor => distractor.Text).ToArray())).ToArray(),
        concept.OrderingActivities.OrderBy(item => item.CreatedAtUtc)
            .Select(item => new OrderingActivityResponse(item.Id, item.Instruction, item.IsActive,
                item.Items.OrderBy(entry => entry.Position).Select(entry => entry.Text).ToArray())).ToArray());
}
