using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.DTOs.Concepts;
using StudyPlatform.Api.DTOs.Modules;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.Exceptions;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Models.Activities;
using StudyPlatform.Api.Services.Learning;

namespace StudyPlatform.Api.Services.Modules;

public sealed partial class ModuleService(
    ApplicationDbContext db,
    PrerequisiteService prerequisiteService,
    TimeProvider timeProvider)
{
    private DateTime _now => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<ModuleSummaryResponse>> ListAsync(Guid teacherId, CancellationToken ct)
    {
        return await db.Modules.AsNoTracking()
            .Where(module => module.TeacherId == teacherId)
            .OrderByDescending(module => module.UpdatedAtUtc)
            .Select(module => new ModuleSummaryResponse(module.Id, module.Title, module.Description,
                module.Subject, module.Version, module.Status, module.CreatedAtUtc, module.UpdatedAtUtc,
                module.Concepts.Count(concept => concept.IsActive)))
            .ToListAsync(ct);
    }

    public async Task<ModuleDetailsResponse> GetAsync(Guid teacherId, Guid moduleId, CancellationToken ct)
    {
        var module = await LoadModuleAsync(teacherId, moduleId, ct);
        return MapModule(module);
    }

    public async Task<ModuleDetailsResponse> CreateAsync(Guid teacherId, CreateModuleRequest request, CancellationToken ct)
    {
        ValidateModuleRequest(request);
        var now = _now;
        var module = new Module
        {
            TeacherId = teacherId,
            Title = request.Title.Trim(),
            Description = CleanOptional(request.Description),
            Subject = request.Subject.Trim(),
            Version = request.Version,
            Status = ModuleStatus.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.Modules.Add(module);
        await db.SaveChangesAsync(ct);
        return MapModule(module);
    }

    public async Task<ModuleDetailsResponse> UpdateAsync(Guid teacherId, Guid moduleId, UpdateModuleRequest request, CancellationToken ct)
    {
        ValidateModuleRequest(request);
        var module = await LoadModuleAsync(teacherId, moduleId, ct);
        module.Title = request.Title.Trim();
        module.Description = CleanOptional(request.Description);
        module.Subject = request.Subject.Trim();
        module.Version = request.Version;
        module.UpdatedAtUtc = _now;
        await db.SaveChangesAsync(ct);
        return MapModule(module);
    }

    public async Task<ModuleDetailsResponse> DuplicateAsync(Guid teacherId, Guid moduleId, CancellationToken ct)
    {
        var source = await LoadModuleAsync(teacherId, moduleId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = _now;
        var copy = new Module
        {
            TeacherId = teacherId,
            Title = AddCopySuffix(source.Title),
            Description = source.Description,
            Subject = source.Subject,
            Version = source.Version,
            Status = ModuleStatus.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        var conceptMap = source.Concepts.ToDictionary(concept => concept.Id, _ => Guid.NewGuid());
        foreach (var concept in source.Concepts)
        {
            copy.Concepts.Add(CloneConcept(concept, conceptMap[concept.Id], copy.Id, now));
        }
        foreach (var concept in source.Concepts)
        {
            foreach (var prerequisite in concept.Prerequisites)
            {
                if (conceptMap.TryGetValue(prerequisite.PrerequisiteConceptId, out var copiedPrerequisiteId))
                {
                    copy.Concepts.Single(item => item.Id == conceptMap[concept.Id]).Prerequisites.Add(
                        new ConceptPrerequisite
                        {
                            ConceptId = conceptMap[concept.Id],
                            PrerequisiteConceptId = copiedPrerequisiteId,
                            ModuleId = copy.Id,
                        });
                }
            }
        }

        db.Modules.Add(copy);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return MapModule(copy);
    }

    public async Task ArchiveAsync(Guid teacherId, Guid moduleId, CancellationToken ct)
    {
        var module = await OwnedModuleQuery(teacherId, moduleId).SingleOrDefaultAsync(ct)
            ?? throw NotFound("module_not_found", "O m\u00F3dulo n\u00E3o foi encontrado.");
        module.Status = ModuleStatus.Archived;
        module.UpdatedAtUtc = _now;
        await db.SaveChangesAsync(ct);
    }

    private async Task<Module> LoadModuleAsync(Guid teacherId, Guid moduleId, CancellationToken ct)
    {
        var module = await ContentGraphQuery(teacherId, moduleId).SingleOrDefaultAsync(ct)
            ?? throw NotFound("module_not_found", "O m\u00F3dulo n\u00E3o foi encontrado.");
        return module;
    }

    private IQueryable<Module> OwnedModuleQuery(Guid teacherId, Guid moduleId) =>
        db.Modules.Where(item => item.TeacherId == teacherId && item.Id == moduleId);

    private IQueryable<Module> ContentGraphQuery(Guid teacherId, Guid moduleId) => OwnedModuleQuery(teacherId, moduleId)
        .Include(item => item.Concepts).ThenInclude(item => item.Keywords)
        .Include(item => item.Concepts).ThenInclude(item => item.Clues)
        .Include(item => item.Concepts).ThenInclude(item => item.Prerequisites)
        .Include(item => item.Concepts).ThenInclude(item => item.RecognitionActivities)
        .Include(item => item.Concepts).ThenInclude(item => item.FillBlankActivities).ThenInclude(item => item.Answers)
        .Include(item => item.Concepts).ThenInclude(item => item.FillBlankActivities).ThenInclude(item => item.Distractors)
        .Include(item => item.Concepts).ThenInclude(item => item.FillBlankActivities)
        .Include(item => item.Concepts).ThenInclude(item => item.OrderingActivities).ThenInclude(item => item.Items)
        .Include(item => item.Concepts).ThenInclude(item => item.OrderingActivities);

    private static ModuleDetailsResponse MapModule(Module module) => new(
        module.Id, module.Title, module.Description, module.Subject, module.Version, module.Status,
        module.CreatedAtUtc, module.UpdatedAtUtc,
        module.Concepts.OrderBy(item => item.Name).Select(ConceptResponseMapper.Map).ToArray());

    private static Concept CloneConcept(Concept source, Guid newId, Guid moduleId, DateTime now)
    {
        var copy = new Concept { Id = newId, ModuleId = moduleId, ExternalId = source.ExternalId,
            Name = source.Name, Definition = source.Definition, IsActive = source.IsActive,
            CreatedAtUtc = now, UpdatedAtUtc = now };
        foreach (var item in source.Keywords) copy.Keywords.Add(new ConceptKeyword { ConceptId = newId, Value = item.Value, IsActive = item.IsActive });
        foreach (var item in source.Clues) copy.Clues.Add(new ConceptClue { ConceptId = newId, Position = item.Position, Text = item.Text, IsActive = item.IsActive });
        foreach (var item in source.RecognitionActivities)
            copy.RecognitionActivities.Add(new RecognitionActivity { ConceptId = newId, Statement = item.Statement,
                IsCorrect = item.IsCorrect, Explanation = item.Explanation, IsActive = item.IsActive, CreatedAtUtc = now, UpdatedAtUtc = now });
        foreach (var item in source.FillBlankActivities)
        {
            var activity = new FillBlankActivity { ConceptId = newId, Text = item.Text, IsActive = item.IsActive, CreatedAtUtc = now, UpdatedAtUtc = now };
            foreach (var answer in item.Answers) activity.Answers.Add(new FillBlankAnswer { SlotNumber = answer.SlotNumber, CorrectText = answer.CorrectText });
            foreach (var distractor in item.Distractors) activity.Distractors.Add(new FillBlankDistractor { Text = distractor.Text });
            copy.FillBlankActivities.Add(activity);
        }
        foreach (var item in source.OrderingActivities)
        {
            var activity = new OrderingActivity { ConceptId = newId, Instruction = item.Instruction, IsActive = item.IsActive, CreatedAtUtc = now, UpdatedAtUtc = now };
            foreach (var entry in item.Items) activity.Items.Add(new OrderingActivityItem { Position = entry.Position, Text = entry.Text });
            copy.OrderingActivities.Add(activity);
        }
        return copy;
    }

    private static string AddCopySuffix(string value)
    {
        const string suffix = " (c\u00F3pia)";
        return value.Length + suffix.Length <= 160 ? value + suffix : value[..(160 - suffix.Length)] + suffix;
    }

    private static void ValidateModuleRequest(CreateModuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) throw BadRequest("invalid_title", "O título do módulo é obrigatório.");
        if (string.IsNullOrWhiteSpace(request.Subject)) throw BadRequest("invalid_subject", "A disciplina é obrigatória.");
    }

    private static void ValidateConceptRequest(SaveConceptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw BadRequest("invalid_concept_name", "O nome do conceito é obrigatório.");
        if (string.IsNullOrWhiteSpace(request.Definition)) throw BadRequest("invalid_definition", "A definição do conceito é obrigatória.");
        if (request.Keywords.Any(value => value is null || value.Length > 200))
            throw BadRequest("invalid_keyword", "Cada palavra-chave pode possuir no máximo 200 caracteres.");
        if (request.Clues.Any(value => value is null || value.Length > 1000))
            throw BadRequest("invalid_clue", "Cada pista pode possuir no máximo 1000 caracteres.");
    }
    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ApiException NotFound(string code, string message) => new((int)HttpStatusCode.NotFound, code, message);
    private static ApiException BadRequest(string code, string message) => new((int)HttpStatusCode.BadRequest, code, message);
}

















