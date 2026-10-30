using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Learning;
using StudyPlatform.Api.Exceptions;

namespace StudyPlatform.Api.Services.Learning;

public sealed class StudentCrosswordService(ApplicationDbContext db, StudentStudyCatalogService catalog)
{
    public async Task<IReadOnlyList<StudentCrosswordEntryResponse>> GetAsync(Guid studentId, Guid moduleId, bool allWords, CancellationToken ct)
    {
        var moduleSummary = await catalog.GetModuleAsync(studentId, moduleId, ct);
        if (!moduleSummary.CrosswordEnabled)
            throw new ApiException(403, "crossword_disabled", "A cruzadinha não está habilitada neste módulo.");
        if (allWords && !moduleSummary.CrosswordAllowAllWords)
            throw new ApiException(403, "crossword_all_words_disabled", "O professor não permitiu o uso de todas as palavras.");

        var query = db.Concepts.AsNoTracking()
            .Where(concept => concept.ModuleId == moduleId && concept.IsActive
                && concept.CrosswordWord != null && concept.CrosswordClue != null);
        if (!allWords)
            query = query.Where(concept => db.StudentConceptStates.Any(state => state.StudentId == studentId
                && state.ConceptId == concept.Id && state.LearningState == LearningState.Mastered));

        return await query.OrderBy(concept => concept.Id)
            .Select(concept => new StudentCrosswordEntryResponse(concept.Id, concept.CrosswordWord!, concept.CrosswordClue!))
            .ToListAsync(ct);
    }
}
