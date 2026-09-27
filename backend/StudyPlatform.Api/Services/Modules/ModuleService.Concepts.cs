using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.DTOs.Concepts;
using StudyPlatform.Api.DTOs.Modules;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.Exceptions;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Models.Activities;
using StudyPlatform.Api.Services.Learning;

namespace StudyPlatform.Api.Services.Modules;

public sealed partial class ModuleService
{    public async Task<ConceptDetailsResponse> CreateConceptAsync(Guid teacherId, Guid moduleId, SaveConceptRequest request, CancellationToken ct)
    {
        ValidateConceptRequest(request);
        var module = await OwnedModuleQuery(teacherId, moduleId).SingleOrDefaultAsync(ct)
            ?? throw NotFound("module_not_found", "O m\u00F3dulo n\u00E3o foi encontrado.");
        var concept = new Concept { ModuleId = module.Id, Name = request.Name.Trim(), Definition = request.Definition.Trim(), CreatedAtUtc = _now, UpdatedAtUtc = _now };
        ValidatePrerequisites(module, concept.Id, request.PrerequisiteIds);
        ApplyConceptContent(concept, request, _now);
        db.Concepts.Add(concept);
        module.UpdatedAtUtc = _now;
        await db.SaveChangesAsync(ct);
        return ConceptResponseMapper.Map(concept);
    }

    public async Task<IReadOnlyList<ConceptSummaryResponse>> ListConceptsAsync(Guid teacherId, Guid moduleId, CancellationToken ct)
    {
        await EnsureOwnedModuleAsync(teacherId, moduleId, ct);
        return await db.Concepts.AsNoTracking().Where(concept => concept.ModuleId == moduleId)
            .OrderBy(concept => concept.Name)
            .Select(concept => new ConceptSummaryResponse(concept.Id, concept.Name, concept.IsActive, concept.Prerequisites.Count))
            .ToListAsync(ct);
    }

    public async Task<ConceptDetailsResponse> GetConceptAsync(Guid teacherId, Guid moduleId, Guid conceptId, CancellationToken ct)
    {
        var concept = await LoadConceptAsync(teacherId, moduleId, conceptId, ct);
        return ConceptResponseMapper.Map(concept);
    }

    public async Task<ConceptDetailsResponse> UpdateConceptAsync(Guid teacherId, Guid moduleId, Guid conceptId, SaveConceptRequest request, CancellationToken ct)
    {
        ValidateConceptRequest(request);
        var concept = await LoadConceptAsync(teacherId, moduleId, conceptId, ct);
        var module = await OwnedModuleQuery(teacherId, moduleId).SingleAsync(ct);
        ValidatePrerequisites(module, concept.Id, request.PrerequisiteIds);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        concept.Name = request.Name.Trim();
        concept.Definition = request.Definition.Trim();
        concept.UpdatedAtUtc = _now;
        ApplyConceptContent(concept, request, _now);
        module.UpdatedAtUtc = _now;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ConceptResponseMapper.Map(concept);
    }

    public async Task<ConceptDetailsResponse> DuplicateConceptAsync(Guid teacherId, Guid moduleId, Guid conceptId, CancellationToken ct)
    {
        var source = await LoadConceptAsync(teacherId, moduleId, conceptId, ct);
        var module = await OwnedModuleQuery(teacherId, moduleId).SingleAsync(ct);
        var copy = CloneConcept(source, Guid.NewGuid(), moduleId, _now);
        copy.Name = AddCopySuffix(source.Name);
        foreach (var prerequisite in source.Prerequisites)
        {
            copy.Prerequisites.Add(new ConceptPrerequisite
            {
                ConceptId = copy.Id,
                PrerequisiteConceptId = prerequisite.PrerequisiteConceptId,
                ModuleId = moduleId,
            });
        }
        db.Concepts.Add(copy);
        module.UpdatedAtUtc = _now;
        await db.SaveChangesAsync(ct);
        return ConceptResponseMapper.Map(copy);
    }

    public async Task DeactivateConceptAsync(Guid teacherId, Guid moduleId, Guid conceptId, CancellationToken ct)
    {
        var concept = await LoadConceptAsync(teacherId, moduleId, conceptId, ct);
        concept.IsActive = false;
        concept.UpdatedAtUtc = _now;
foreach (var clue in concept.Clues) clue.IsActive = false;
        foreach (var activity in concept.RecognitionActivities) activity.IsActive = false;
        foreach (var activity in concept.FillBlankActivities) activity.IsActive = false;
        foreach (var activity in concept.OrderingActivities) activity.IsActive = false;
        var module = await OwnedModuleQuery(teacherId, moduleId).SingleAsync(ct);
        module.UpdatedAtUtc = _now;
        await db.SaveChangesAsync(ct);
    }

    public async Task<PublicationValidationResponse> ValidatePublicationAsync(Guid teacherId, Guid moduleId, CancellationToken ct)
    {
        var module = await OwnedModuleQuery(teacherId, moduleId)
            .Include(item => item.Concepts).ThenInclude(item => item.Keywords)
            .Include(item => item.Concepts).ThenInclude(item => item.Clues)
            .Include(item => item.Concepts).ThenInclude(item => item.RecognitionActivities)
            .Include(item => item.Concepts).ThenInclude(item => item.FillBlankActivities).ThenInclude(item => item.Answers)
            .Include(item => item.Concepts).ThenInclude(item => item.FillBlankActivities).ThenInclude(item => item.Distractors)
            .Include(item => item.Concepts).ThenInclude(item => item.OrderingActivities).ThenInclude(item => item.Items)
            .Include(item => item.Concepts).ThenInclude(item => item.Prerequisites)
            .ThenInclude(item => item.PrerequisiteConcept)
            .SingleOrDefaultAsync(ct)
            ?? throw NotFound("module_not_found", "O m\u00F3dulo n\u00E3o foi encontrado.");

        var issues = new List<PublicationIssue>();
        if (module.Status == ModuleStatus.Archived)
            issues.Add(new(null, "module_archived", "Um m\u00F3dulo arquivado n\u00E3o pode ser publicado."));

        foreach (var concept in module.Concepts.Where(item => item.IsActive))
        {
            if (string.IsNullOrWhiteSpace(concept.Name)) issues.Add(new(concept.Id, "missing_name", "O conceito precisa de um nome."));
            if (string.IsNullOrWhiteSpace(concept.Definition)) issues.Add(new(concept.Id, "missing_definition", "O conceito precisa de uma defini\u00E7\u00E3o."));
            if (concept.Keywords.Count(item => item.IsActive && !string.IsNullOrWhiteSpace(item.Value)) < 1) issues.Add(new(concept.Id, "missing_keywords", "O conceito precisa possuir pelo menos 1 palavra-chave."));
            if (concept.Clues.Count(item => item.IsActive && !string.IsNullOrWhiteSpace(item.Text)) < 3) issues.Add(new(concept.Id, "missing_clues", "O conceito precisa possuir pelo menos 3 pistas."));
            if (concept.RecognitionActivities.Count(item => item.IsActive) < 3) issues.Add(new(concept.Id, "missing_true_false_activities", "O conceito precisa possuir pelo menos 3 atividades verdadeiro/falso ativas."));
            if (concept.RecognitionActivities.Any(item => item.IsActive && (string.IsNullOrWhiteSpace(item.Statement) || string.IsNullOrWhiteSpace(item.Explanation))))
                issues.Add(new(concept.Id, "invalid_true_false_activity", "Afirmação e explicação de verdadeiro/falso são obrigatórias."));
            var activeFillBlanks = concept.FillBlankActivities.Where(item => item.IsActive).ToArray();
            if (activeFillBlanks.Length < 3) issues.Add(new(concept.Id, "missing_fill_blank_activities", "O conceito precisa possuir pelo menos 3 atividades de lacunas ativas."));
            foreach (var activity in activeFillBlanks)
                if (!IsValidFillBlank(activity)) issues.Add(new(concept.Id, "invalid_fill_blank_activity", "Uma atividade de lacunas possui slots, respostas ou distratores incoerentes."));
            foreach (var activity in concept.OrderingActivities.Where(item => item.IsActive))
                if (activity.Items.Count < 2 || activity.Items.Any(item => string.IsNullOrWhiteSpace(item.Text)))
                    issues.Add(new(concept.Id, "invalid_ordering_activity", "Cada atividade de ordena\u00E7\u00E3o ativa precisa possuir pelo menos dois itens v\u00E1lidos."));
            foreach (var edge in concept.Prerequisites)
            {
                if (edge.ModuleId != module.Id || edge.PrerequisiteConcept.ModuleId != module.Id)
                    issues.Add(new(concept.Id, "prerequisite_outside_module", "Todos os pr\u00E9-requisitos devem pertencer ao mesmo m\u00F3dulo."));
                if (edge.PrerequisiteConceptId == concept.Id)
                    issues.Add(new(concept.Id, "self_prerequisite", "Um conceito n\u00E3o pode ser pr\u00E9-requisito de si pr\u00F3prio."));
                if (!edge.PrerequisiteConcept.IsActive)
                    issues.Add(new(concept.Id, "inactive_prerequisite", "Um conceito ativo n\u00E3o pode depender de um conceito desativado."));
            }
        }

        var graph = module.Concepts.Where(concept => concept.IsActive).Select(concept => new ConceptPrerequisiteNode(concept.Id,
            concept.Prerequisites.Select(edge => edge.PrerequisiteConceptId).ToArray()));
        var cycle = prerequisiteService.FindCycle(graph);
        if (cycle is not null) issues.Add(new(null, "prerequisite_cycle", $"H\u00E1 um ciclo de pr\u00E9-requisitos: {cycle}."));
        return new PublicationValidationResponse(issues.Count == 0, issues);
    }

    public async Task<ModuleDetailsResponse> PublishAsync(Guid teacherId, Guid moduleId, CancellationToken ct)
    {
        var validation = await ValidatePublicationAsync(teacherId, moduleId, ct);
        if (!validation.IsValid)
            throw new ApiException((int)HttpStatusCode.UnprocessableEntity, "module_validation_error",
                "N\u00E3o foi poss\u00EDvel publicar o m\u00F3dulo.", new Dictionary<string, string[]> { ["errors"] = validation.Errors.Select(issue => issue.Message).ToArray() });
        var module = await OwnedModuleQuery(teacherId, moduleId).SingleAsync(ct);
        module.Status = ModuleStatus.Published;
        module.UpdatedAtUtc = _now;
        await db.SaveChangesAsync(ct);
        return await GetAsync(teacherId, moduleId, ct);
    }

    private void ValidatePrerequisites(Module module, Guid conceptId, IReadOnlyCollection<Guid> requestedIds)
    {
        var conceptIds = db.Concepts.Where(item => item.ModuleId == module.Id).Select(item => item.Id).ToHashSet();
        var referenceIssue = prerequisiteService.ValidateReferences(conceptId, conceptIds, requestedIds).FirstOrDefault();
        if (referenceIssue is not null) throw BadRequest(referenceIssue.Code, referenceIssue.Message);

        var edges = db.ConceptPrerequisites.Where(edge => edge.ModuleId == module.Id && edge.ConceptId != conceptId)
            .Select(edge => new { edge.ConceptId, edge.PrerequisiteConceptId }).ToList();
        var graph = conceptIds.Select(id => new ConceptPrerequisiteNode(id,
            id == conceptId ? requestedIds.ToArray() : edges.Where(edge => edge.ConceptId == id)
                .Select(edge => edge.PrerequisiteConceptId).ToArray())).ToList();
        if (graph.All(node => node.Id != conceptId))
            graph.Add(new ConceptPrerequisiteNode(conceptId, requestedIds.ToArray()));
        var cycle = prerequisiteService.FindCycle(graph);
        if (cycle is not null)
            throw BadRequest("prerequisite_cycle", $"A altera\u00E7\u00E3o criaria um ciclo de pr\u00E9-requisitos: {cycle}.");
    }
    private void ApplyConceptContent(Concept concept, SaveConceptRequest request, DateTime now)
    {
        var values = request.Keywords.Select(value => value.Trim()).Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var keyword in concept.Keywords)
            keyword.IsActive = values.Contains(keyword.Value, StringComparer.OrdinalIgnoreCase);
        var existingValues = concept.Keywords.Select(keyword => keyword.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values.Where(value => !existingValues.Contains(value)))
        {
            var keyword = new ConceptKeyword { ConceptId = concept.Id, Value = value };
            db.ConceptKeywords.Add(keyword);
            concept.Keywords.Add(keyword);
        }

        var cluesByPosition = concept.Clues.ToDictionary(clue => clue.Position);
        for (var position = 0; position < request.Clues.Count; position++)
        {
            var text = request.Clues[position].Trim();
            if (text.Length == 0) throw BadRequest("empty_clue", "As pistas não podem ficar vazias.");
            if (cluesByPosition.TryGetValue(position, out var existing))
            {
                existing.Text = text;
                existing.IsActive = true;
            }
            else
            {
                var clue = new ConceptClue { ConceptId = concept.Id, Position = position, Text = text };
                db.ConceptClues.Add(clue);
                concept.Clues.Add(clue);
            }
        }
        foreach (var clue in concept.Clues.Where(clue => clue.Position >= request.Clues.Count)) clue.IsActive = false;

        var requestedPrerequisites = request.PrerequisiteIds.ToHashSet();
        foreach (var existing in concept.Prerequisites.ToArray())
            if (!requestedPrerequisites.Contains(existing.PrerequisiteConceptId)) concept.Prerequisites.Remove(existing);
        var existingIds = concept.Prerequisites.Select(item => item.PrerequisiteConceptId).ToHashSet();
        foreach (var id in requestedPrerequisites.Except(existingIds))
        {
            var prerequisite = new ConceptPrerequisite { ConceptId = concept.Id, PrerequisiteConceptId = id, ModuleId = concept.ModuleId };
            db.ConceptPrerequisites.Add(prerequisite);
            concept.Prerequisites.Add(prerequisite);
        }

        ApplyRecognitionActivities(concept, request.RecognitionActivities, now);
        ApplyFillBlankActivities(concept, request.FillBlankActivities, now);
        ApplyOrderingActivities(concept, request.OrderingActivities, now);
    }

    private void ApplyRecognitionActivities(Concept concept, IReadOnlyCollection<RecognitionActivityInput> inputs, DateTime now)
    {
        var existing = concept.RecognitionActivities.ToDictionary(item => item.Id);
        var kept = new HashSet<Guid>();
        foreach (var input in inputs)
        {
            var item = input.Id is Guid id
                ? existing.TryGetValue(id, out var found) ? found : throw BadRequest("invalid_activity_id", "Uma atividade n\u00E3o pertence a este conceito.")
                : new RecognitionActivity { ConceptId = concept.Id, CreatedAtUtc = now };
            item.Statement = input.Statement.Trim(); item.IsCorrect = input.IsCorrect;
            item.Explanation = input.Explanation.Trim(); item.IsActive = true; item.UpdatedAtUtc = now;
            if (input.Id is null)
            {
                db.RecognitionActivities.Add(item);
                concept.RecognitionActivities.Add(item);
            }
            kept.Add(item.Id);
        }
        foreach (var item in concept.RecognitionActivities.Where(item => !kept.Contains(item.Id))) item.IsActive = false;
    }

    private void ApplyFillBlankActivities(Concept concept, IReadOnlyCollection<FillBlankActivityInput> inputs, DateTime now)
    {
        var existing = concept.FillBlankActivities.ToDictionary(item => item.Id);
        var kept = new HashSet<Guid>();
        foreach (var input in inputs)
        {
            var item = input.Id is Guid id
                ? existing.TryGetValue(id, out var found) ? found : throw BadRequest("invalid_activity_id", "Uma atividade não pertence a este conceito.")
                : new FillBlankActivity { ConceptId = concept.Id, CreatedAtUtc = now };
            item.Text = input.Text.Trim(); item.IsActive = true; item.UpdatedAtUtc = now;
            if (input.Answers.Select(answer => answer.SlotNumber).Distinct().Count() != input.Answers.Count)
                throw BadRequest("duplicate_fill_blank_slot", "Cada slot de lacuna deve possuir uma única resposta.");
            var answers = item.Answers.ToDictionary(answer => answer.SlotNumber);
            var answerSlots = input.Answers.Select(answer => answer.SlotNumber).ToHashSet();
            foreach (var answer in item.Answers.Where(answer => !answerSlots.Contains(answer.SlotNumber)).ToArray())
            {
                db.FillBlankAnswers.Remove(answer);
                item.Answers.Remove(answer);
            }
            foreach (var answerInput in input.Answers)
            {
                if (answers.TryGetValue(answerInput.SlotNumber, out var answer)) answer.CorrectText = answerInput.CorrectText.Trim();
                else
                {
                    var newAnswer = new FillBlankAnswer { FillBlankActivityId = item.Id, SlotNumber = answerInput.SlotNumber, CorrectText = answerInput.CorrectText.Trim() };
                    db.FillBlankAnswers.Add(newAnswer);
                    item.Answers.Add(newAnswer);
                }
            }
            var distractorValues = input.Distractors.Select(value => value.Trim()).Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var distractor in item.Distractors.Where(distractor => !distractorValues.Contains(distractor.Text)).ToArray())
            {
                db.FillBlankDistractors.Remove(distractor);
                item.Distractors.Remove(distractor);
            }
            var currentDistractors = item.Distractors.Select(distractor => distractor.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var distractor in distractorValues.Where(value => !currentDistractors.Contains(value)))
            {
                var entry = new FillBlankDistractor { FillBlankActivityId = item.Id, Text = distractor };
                db.FillBlankDistractors.Add(entry);
                item.Distractors.Add(entry);
            }
            if (input.Id is null)
            {
                db.FillBlankActivities.Add(item);
                concept.FillBlankActivities.Add(item);
            }
            kept.Add(item.Id);
        }
        foreach (var item in concept.FillBlankActivities.Where(item => !kept.Contains(item.Id))) item.IsActive = false;
    }

    private void ApplyOrderingActivities(Concept concept, IReadOnlyCollection<OrderingActivityInput> inputs, DateTime now)
    {
        var existing = concept.OrderingActivities.ToDictionary(item => item.Id);
        var kept = new HashSet<Guid>();
        foreach (var input in inputs)
        {
            var item = input.Id is Guid id
                ? existing.TryGetValue(id, out var found) ? found : throw BadRequest("invalid_activity_id", "Uma atividade não pertence a este conceito.")
                : new OrderingActivity { ConceptId = concept.Id, CreatedAtUtc = now };
            item.Instruction = input.Instruction.Trim(); item.IsActive = true; item.UpdatedAtUtc = now;
            if (input.Items.Count != input.Items.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                throw BadRequest("duplicate_ordering_item", "Os itens da ordenação devem ser distintos.");
            var itemByPosition = item.Items.ToDictionary(entry => entry.Position);
            for (var position = 0; position < input.Items.Count; position++)
            {
                var text = input.Items[position].Trim();
                if (itemByPosition.TryGetValue(position, out var existingItem)) existingItem.Text = text;
                else
                {
                    var orderingItem = new OrderingActivityItem { OrderingActivityId = item.Id, Position = position, Text = text };
                    db.OrderingActivityItems.Add(orderingItem);
                    item.Items.Add(orderingItem);
                }
            }
            foreach (var oldItem in item.Items.Where(entry => entry.Position >= input.Items.Count).ToArray())
            {
                db.OrderingActivityItems.Remove(oldItem);
                item.Items.Remove(oldItem);
            }
            if (input.Id is null)
            {
                db.OrderingActivities.Add(item);
                concept.OrderingActivities.Add(item);
            }
            kept.Add(item.Id);
        }
        foreach (var item in concept.OrderingActivities.Where(item => !kept.Contains(item.Id))) item.IsActive = false;
    }

    private static bool IsValidFillBlank(FillBlankActivity activity)
    {
        var slots = Regex.Matches(activity.Text, "\\{\\{(\\d+)\\}\\}")
            .Select(match => int.TryParse(match.Groups[1].Value, out var number) ? number : -1)
            .Distinct().Order().ToArray();
        var answerSlots = activity.Answers.Select(answer => answer.SlotNumber).Distinct().Order().ToArray();
        return slots.Length > 0 && slots.SequenceEqual(Enumerable.Range(1, slots.Length))
            && slots.SequenceEqual(answerSlots)
            && activity.Answers.Count == answerSlots.Length
            && activity.Answers.All(answer => !string.IsNullOrWhiteSpace(answer.CorrectText))
            && activity.Distractors.Count > 0
            && activity.Distractors.All(item => !string.IsNullOrWhiteSpace(item.Text));
    }

    private async Task<Concept> LoadConceptAsync(Guid teacherId, Guid moduleId, Guid conceptId, CancellationToken ct)
    {
        var moduleExists = await OwnedModuleQuery(teacherId, moduleId).AnyAsync(ct);
        if (!moduleExists) throw NotFound("module_not_found", "O m\u00F3dulo n\u00E3o foi encontrado.");
        return await ConceptGraphQuery().Where(item => item.Id == conceptId && item.ModuleId == moduleId)
            .SingleOrDefaultAsync(ct) ?? throw NotFound("concept_not_found", "O conceito n\u00E3o foi encontrado.");
    }

    private async Task EnsureOwnedModuleAsync(Guid teacherId, Guid moduleId, CancellationToken ct)
    {
        if (!await OwnedModuleQuery(teacherId, moduleId).AnyAsync(ct))
            throw NotFound("module_not_found", "O m\u00F3dulo n\u00E3o foi encontrado.");
    }

    private IQueryable<Concept> ConceptGraphQuery() => db.Concepts
        .Include(item => item.Keywords).Include(item => item.Clues).Include(item => item.Prerequisites)
        .Include(item => item.RecognitionActivities)
        .Include(item => item.FillBlankActivities).ThenInclude(item => item.Answers)
        .Include(item => item.FillBlankActivities).ThenInclude(item => item.Distractors)
        .Include(item => item.FillBlankActivities)
        .Include(item => item.OrderingActivities).ThenInclude(item => item.Items)
        .Include(item => item.OrderingActivities);


}


