using System.Text.Json.Serialization;

namespace StudyPlatform.Api.DTOs.Modules.ImportExport;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ModuleExchangeDocument
{
    public required int SchemaVersion { get; init; }
    public required ModuleExchange Module { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ModuleExchange
{
    public required string ExternalId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string Subject { get; init; }
    public required int Version { get; init; }
    public required List<ConceptExchange> Concepts { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ConceptExchange
{
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
    public required string Definition { get; init; }
    public bool IsActive { get; init; } = true;
    public required List<KeywordExchange> Keywords { get; init; }
    public required List<ClueExchange> Clues { get; init; }
    public required List<string> Prerequisites { get; init; }
    public required List<TrueFalseExchange> TrueFalseActivities { get; init; }
    public required List<FillBlankExchange> FillBlankActivities { get; init; }
    public required List<OrderingExchange> OrderingActivities { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class KeywordExchange
{
    public required string Value { get; init; }
    public bool IsActive { get; init; } = true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ClueExchange
{
    public required string Text { get; init; }
    public bool IsActive { get; init; } = true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class TrueFalseExchange
{
    public required string Statement { get; init; }
    public required bool IsCorrect { get; init; }
    public required string Explanation { get; init; }
    public bool IsActive { get; init; } = true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class FillBlankExchange
{
    public required string Text { get; init; }
    public required List<FillBlankAnswerExchange> Answers { get; init; }
    public required List<string> Distractors { get; init; }
    public bool IsActive { get; init; } = true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class FillBlankAnswerExchange
{
    public required int SlotNumber { get; init; }
    public required string CorrectText { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class OrderingExchange
{
    public required string Instruction { get; init; }
    public required List<string> Items { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record ModuleExportFile(string FileName, byte[] Content);
