using System.Text.Json;

namespace StudyPlatform.Api.DTOs.Learning;

public sealed record StartStudySessionResponse(Guid SessionId, string Status, int TotalActivities, int CompletedActivities, PresentedActivityResponse? Activity);
public sealed record StudySessionResponse(Guid SessionId, string Status, int TotalActivities, int CompletedActivities, DateTime StartedAtUtc, DateTime? CompletedAtUtc, PresentedActivityResponse? Activity);
public sealed record PresentedActivityResponse(Guid PresentationId, string Type, Guid ConceptId, string ConceptName, DateTime StartedAtUtc, JsonElement Payload);
public sealed record RevealHintResponse(int RevealedCount, int TotalHints, string Text);
public sealed record SubmitActivityAnswerRequest(Guid PresentationId, string Type, JsonElement Answer, int? AttemptsUsed, int? HintsUsed);
public sealed record ActivityAnswerResponse(bool WasCorrect, string Feedback, string LearningState, int CompletedActivities, int TotalActivities, string SessionStatus, PresentedActivityResponse? NextActivity, JsonElement? CorrectAnswer);
public sealed record ExposureAnswer(bool Completed);
public sealed record TrueFalseAnswer(bool Choice);
public sealed record FillBlankAnswerInput(int SlotNumber, string Text);
public sealed record FillBlankAnswerPayload(IReadOnlyList<FillBlankAnswerInput> Answers);
public sealed record GuessConceptAnswer(string Text);
public sealed record OrderingAnswer(IReadOnlyList<Guid> ItemIds);
