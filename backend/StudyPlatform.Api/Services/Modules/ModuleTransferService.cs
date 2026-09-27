using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Domain.Enums;
using StudyPlatform.Api.DTOs.Concepts;
using StudyPlatform.Api.DTOs.Modules;
using StudyPlatform.Api.DTOs.Modules.ImportExport;
using StudyPlatform.Api.Exceptions;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Models.Activities;
using StudyPlatform.Api.Services.Learning;

namespace StudyPlatform.Api.Services.Modules;

public sealed class ModuleTransferService(
    ApplicationDbContext db,
    PrerequisiteService prerequisites,
    TimeProvider clock)
{
    private const int CurrentSchemaVersion = 1;
    public const int MaximumImportBytes = 1_048_576;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public async Task<ModuleExportFile> ExportAsync(Guid teacherId, Guid moduleId, CancellationToken ct)
    {
        var module = await ContentGraph(teacherId, moduleId).AsNoTrackingWithIdentityResolution().AsSplitQuery().SingleOrDefaultAsync(ct)
            ?? throw NotFound();
        var externalIds = CreateExternalIds(module.Concepts);
        var document = new ModuleExchangeDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            Module = new ModuleExchange
            {
                ExternalId = "module",
                Title = module.Title,
                Description = module.Description,
                Subject = module.Subject,
                Version = module.Version,
                Concepts = module.Concepts.OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id).Select(c => new ConceptExchange
                {
                    ExternalId = externalIds[c.Id],
                    Name = c.Name,
                    Definition = c.Definition,
                    IsActive = c.IsActive,
                    Keywords = c.Keywords.OrderBy(k => k.Value, StringComparer.Ordinal)
                        .Select(k => new KeywordExchange { Value = k.Value, IsActive = k.IsActive }).ToList(),
                    Clues = c.Clues.OrderBy(k => k.Position)
                        .Select(k => new ClueExchange { Text = k.Text, IsActive = k.IsActive }).ToList(),
                    Prerequisites = c.Prerequisites.Select(edge => externalIds[edge.PrerequisiteConceptId])
                        .Order(StringComparer.Ordinal).ToList(),
                    TrueFalseActivities = c.RecognitionActivities.OrderBy(a => a.CreatedAtUtc).ThenBy(a => a.Id)
                        .Select(a => new TrueFalseExchange { Statement = a.Statement, IsCorrect = a.IsCorrect,
                            Explanation = a.Explanation, IsActive = a.IsActive }).ToList(),
                    FillBlankActivities = c.FillBlankActivities.OrderBy(a => a.CreatedAtUtc).ThenBy(a => a.Id)
                        .Select(a => new FillBlankExchange { Text = a.Text, IsActive = a.IsActive,
                            Answers = a.Answers.OrderBy(answer => answer.SlotNumber)
                                .Select(answer => new FillBlankAnswerExchange { SlotNumber = answer.SlotNumber, CorrectText = answer.CorrectText }).ToList(),
                            Distractors = a.Distractors.OrderBy(item => item.Id).Select(item => item.Text).ToList() }).ToList(),
                    OrderingActivities = c.OrderingActivities.OrderBy(a => a.CreatedAtUtc).ThenBy(a => a.Id)
                        .Select(a => new OrderingExchange { Instruction = a.Instruction, IsActive = a.IsActive,
                            Items = a.Items.OrderBy(item => item.Position).Select(item => item.Text).ToList() }).ToList(),
                }).ToList(),
            },
        };
        var content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document, JsonOptions));
        return new ModuleExportFile($"{SafeFileName(module.Title)}.json", content);
    }

    public async Task<ModuleDetailsResponse> ImportAsync(Guid teacherId, string json, CancellationToken ct)
    {
        ModuleExchangeDocument? document;
        try { document = JsonSerializer.Deserialize<ModuleExchangeDocument>(json, JsonOptions); }
        catch (JsonException ex)
        {
            var path = string.IsNullOrWhiteSpace(ex.Path) ? "$" : ex.Path;
            throw ImportError("invalid_json", "O documento não corresponde ao formato JSON de módulo suportado.",
                new Dictionary<string, string[]> { [path] = [ex.Message] });
        }

        if (document is null) throw ImportError("invalid_json", "O documento JSON está vazio.");
        if (document.SchemaVersion != CurrentSchemaVersion)
            throw new ApiException((int)HttpStatusCode.BadRequest, "unsupported_schema_version",
                $"A versão de schema {document.SchemaVersion} não é suportada. Versão aceita: {CurrentSchemaVersion}.");

        var errors = Validate(document);
        if (errors.Count != 0)
            throw ImportError("import_validation_failed", "O módulo contém problemas de validação.", errors);

        var source = document.Module;
        var now = clock.GetUtcNow().UtcDateTime;
        var imported = new Module
        {
            TeacherId = teacherId, Title = source.Title.Trim(), Description = Clean(source.Description),
            Subject = source.Subject.Trim(), Version = source.Version, Status = ModuleStatus.Draft,
            CreatedAtUtc = now, UpdatedAtUtc = now,
        };
        var concepts = source.Concepts.ToDictionary(x => x.ExternalId, _ => new Concept(), StringComparer.Ordinal);
        foreach (var item in source.Concepts)
        {
            var concept = concepts[item.ExternalId];
            concept.ModuleId = imported.Id;
            concept.ExternalId = item.ExternalId;
            concept.Name = item.Name.Trim();
            concept.Definition = item.Definition.Trim();
            concept.IsActive = item.IsActive;
            concept.CreatedAtUtc = now;
            concept.UpdatedAtUtc = now;
            foreach (var keyword in item.Keywords!)
                concept.Keywords.Add(new ConceptKeyword { Value = keyword.Value.Trim(), IsActive = keyword.IsActive });
            for (var i = 0; i < item.Clues!.Count; i++)
                concept.Clues.Add(new ConceptClue { Position = i, Text = item.Clues[i].Text.Trim(), IsActive = item.Clues[i].IsActive });
            foreach (var activity in item.TrueFalseActivities!)
                concept.RecognitionActivities.Add(new RecognitionActivity
                {
                    Statement = activity.Statement.Trim(), IsCorrect = activity.IsCorrect, Explanation = activity.Explanation.Trim(),
                    IsActive = activity.IsActive, CreatedAtUtc = now, UpdatedAtUtc = now,
                });
            foreach (var activity in item.FillBlankActivities!)
            {
                var fill = new FillBlankActivity { Text = activity.Text.Trim(), IsActive = activity.IsActive, CreatedAtUtc = now, UpdatedAtUtc = now };
                foreach (var answer in activity.Answers!)
                    fill.Answers.Add(new FillBlankAnswer { SlotNumber = answer.SlotNumber, CorrectText = answer.CorrectText.Trim() });
                foreach (var distractor in activity.Distractors!)
                    fill.Distractors.Add(new FillBlankDistractor { Text = distractor.Trim() });
                concept.FillBlankActivities.Add(fill);
            }
            foreach (var activity in item.OrderingActivities!)
            {
                var ordering = new OrderingActivity
                {
                    Instruction = activity.Instruction.Trim(), IsActive = activity.IsActive, CreatedAtUtc = now, UpdatedAtUtc = now,
                };
                for (var i = 0; i < activity.Items!.Count; i++)
                    ordering.Items.Add(new OrderingActivityItem { Position = i, Text = activity.Items[i].Trim() });
                concept.OrderingActivities.Add(ordering);
            }
            imported.Concepts.Add(concept);
        }

        foreach (var item in source.Concepts)
            foreach (var prerequisiteId in item.Prerequisites!)
                concepts[item.ExternalId].Prerequisites.Add(new ConceptPrerequisite
                {
                    ConceptId = concepts[item.ExternalId].Id,
                    PrerequisiteConceptId = concepts[prerequisiteId].Id,
                    ModuleId = imported.Id,
                });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Modules.Add(imported);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new ModuleDetailsResponse(imported.Id, imported.Title, imported.Description, imported.Subject,
            imported.Version, imported.Status, imported.CreatedAtUtc, imported.UpdatedAtUtc,
            imported.Concepts.OrderBy(c => c.Name).Select(ConceptResponseMapper.Map).ToArray());
    }

    private Dictionary<string, string[]> Validate(ModuleExchangeDocument document)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void Add(string path, string code, string message)
        {
            if (!errors.TryGetValue(path, out var messages)) errors[path] = messages = [];
            messages.Add($"{code}: {message}");
        }
        var module = document.Module;
        if (module is null) { Add("module", "required", "O objeto module é obrigatório."); return Arrays(errors); }
        if (string.IsNullOrWhiteSpace(module.ExternalId) || module.ExternalId.Length > 80) Add("module.externalId", "invalid_external_id", "Obrigatório, limite de 80 caracteres.");
        if (string.IsNullOrWhiteSpace(module.Title) || module.Title.Length > 160) Add("module.title", "invalid_title", "Obrigatório, limite de 160 caracteres.");
        if (module.Description?.Length > 4000) Add("module.description", "invalid_description", "O limite é 4000 caracteres.");
        if (string.IsNullOrWhiteSpace(module.Subject) || module.Subject.Length > 120) Add("module.subject", "invalid_subject", "Obrigatória, limite de 120 caracteres.");
        if (module.Version < 1) Add("module.version", "invalid_version", "A versão deve ser positiva.");
        if (module.Concepts is null) { Add("module.concepts", "required", "A lista de conceitos é obrigatória."); return Arrays(errors); }

        var externalIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < module.Concepts.Count; i++)
        {
            var c = module.Concepts[i];
            var path = $"module.concepts[{i}]";
            if (c is null) { Add(path, "required", "O conceito não pode ser nulo."); continue; }
            if (string.IsNullOrWhiteSpace(c.ExternalId) || c.ExternalId.Length > 80) Add($"{path}.externalId", "invalid_external_id", "Obrigatório, limite de 80 caracteres.");
            else if (!externalIds.Add(c.ExternalId)) Add($"{path}.externalId", "duplicate_external_id", "O ID externo deve ser único.");
            if (string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 160) Add($"{path}.name", "invalid_name", "Obrigatório, limite de 160 caracteres.");
            if (string.IsNullOrWhiteSpace(c.Definition) || c.Definition.Length > 12000) Add($"{path}.definition", "invalid_definition", "Obrigatória, limite de 12000 caracteres.");
            CheckKeywords(c, path, Add);
            CheckClues(c, path, Add);
            CheckTrueFalse(c, path, Add);
            CheckFillBlanks(c, path, Add);
            CheckOrdering(c, path, Add);
        }

        if (externalIds.Contains(module.ExternalId)) Add("module.externalId", "duplicate_external_id", "O ID externo do módulo deve ser distinto dos IDs dos conceitos.");
        var ids = externalIds.ToDictionary(id => id, _ => Guid.NewGuid(), StringComparer.Ordinal);
        var graph = new List<ConceptPrerequisiteNode>();
        var moduleConceptIds = ids.Values.ToArray();
        var visitedExternalConcepts = new HashSet<Guid>();
        for (var i = 0; i < module.Concepts.Count; i++)
        {
            var c = module.Concepts[i];
            if (c is null || string.IsNullOrWhiteSpace(c.ExternalId) || !ids.TryGetValue(c.ExternalId, out var id) || !visitedExternalConcepts.Add(id)) continue;
            if (c.Prerequisites is null) { Add($"module.concepts[{i}].prerequisites", "required", "A lista de pré-requisitos é obrigatória."); continue; }
            var requested = new List<Guid>();
            for (var j = 0; j < c.Prerequisites.Count; j++)
            {
                var external = c.Prerequisites[j];
                if (string.IsNullOrWhiteSpace(external) || !ids.TryGetValue(external, out var prerequisite))
                    Add($"module.concepts[{i}].prerequisites[{j}]", "missing_prerequisite_reference", $"O conceito '{external}' não existe neste módulo.");
                else requested.Add(prerequisite);
            }
            foreach (var issue in prerequisites.ValidateReferences(id, moduleConceptIds, requested))
                Add($"module.concepts[{i}].prerequisites", issue.Code, issue.Message);
            graph.Add(new ConceptPrerequisiteNode(id, requested));
        }
        var cycle = prerequisites.FindCycle(graph);
        if (cycle is not null) Add("module.concepts", "prerequisite_cycle", $"Há um ciclo de pré-requisitos: {cycle}.");
        return Arrays(errors);
    }

    private static void CheckKeywords(ConceptExchange c, string path, Action<string,string,string> add)
    {
        if (c.Keywords is null) { add($"{path}.keywords", "required", "A lista de keywords é obrigatória."); return; }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < c.Keywords.Count; i++)
        {
            var x = c.Keywords[i];
            if (x is null || string.IsNullOrWhiteSpace(x.Value) || x.Value.Length > 200) add($"{path}.keywords[{i}]", "invalid_keyword", "Obrigatória, limite de 200 caracteres.");
            else if (!seen.Add(x.Value.Trim())) add($"{path}.keywords[{i}]", "duplicate_keyword", "Keywords duplicadas não são permitidas.");
        }
    }
    private static void CheckClues(ConceptExchange c, string path, Action<string,string,string> add)
    {
        if (c.Clues is null) { add($"{path}.clues", "required", "A lista de pistas é obrigatória."); return; }
        for (var i = 0; i < c.Clues.Count; i++)
            if (c.Clues[i] is null || string.IsNullOrWhiteSpace(c.Clues[i].Text) || c.Clues[i].Text.Length > 1000)
                add($"{path}.clues[{i}]", "invalid_clue", "Obrigatória, limite de 1000 caracteres.");
    }
    private static void CheckTrueFalse(ConceptExchange c, string path, Action<string,string,string> add)
    {
        if (c.TrueFalseActivities is null) { add($"{path}.trueFalseActivities", "required", "A lista de atividades é obrigatória."); return; }
        for (var i = 0; i < c.TrueFalseActivities.Count; i++)
        {
            var x = c.TrueFalseActivities[i];
            if (x is null || string.IsNullOrWhiteSpace(x.Statement) || x.Statement.Length > 2000 ||
                string.IsNullOrWhiteSpace(x.Explanation) || x.Explanation.Length > 2000)
                add($"{path}.trueFalseActivities[{i}]", "invalid_activity", "Afirmação e explicação são obrigatórias, até 2000 caracteres.");
        }
    }
    private static void CheckFillBlanks(ConceptExchange c, string path, Action<string,string,string> add)
    {
        if (c.FillBlankActivities is null) { add($"{path}.fillBlankActivities", "required", "A lista de lacunas é obrigatória."); return; }
        for (var i = 0; i < c.FillBlankActivities.Count; i++)
        {
            var x = c.FillBlankActivities[i];
            var p = $"{path}.fillBlankActivities[{i}]";
            if (x is null) { add(p, "invalid_fill_blank", "Atividade nula."); continue; }
            if (string.IsNullOrWhiteSpace(x.Text) || x.Text.Length > 4000) add($"{p}.text", "invalid_fill_blank", "Obrigatório, limite de 4000 caracteres.");
            if (x.Answers is null) { add($"{p}.answers", "invalid_fill_blank", "Respostas são obrigatórias."); continue; }
            if (x.Distractors is null) { add($"{p}.distractors", "invalid_fill_blank", "Distratores são obrigatórios."); continue; }
            var matches = Regex.Matches(x.Text ?? "", @"\{\{(\d+)\}\}");
            var slots = matches.Select(m => int.TryParse(m.Groups[1].Value, out var n) ? n : -1).Distinct().Order().ToArray();
            var answerSlots = x.Answers.Where(a => a is not null).Select(a => a.SlotNumber).Order().ToArray();
            if (slots.Length == 0 || (x.Text?.Contains("{{", StringComparison.Ordinal) == true && matches.Count != Regex.Matches(x.Text, @"\{\{(\d+)\}\}").Count) ||
                !slots.SequenceEqual(Enumerable.Range(1, slots.Length)) || !slots.SequenceEqual(answerSlots) || answerSlots.Length != x.Answers.Count)
                add(p, "invalid_fill_blank", "Slots e respostas devem corresponder exatamente, sem lacunas ou duplicatas.");
            for (var j = 0; j < x.Answers.Count; j++)
                if (x.Answers[j] is null || string.IsNullOrWhiteSpace(x.Answers[j].CorrectText) || x.Answers[j].CorrectText.Length > 300)
                    add($"{p}.answers[{j}]", "invalid_fill_blank", "Resposta obrigatória de até 300 caracteres.");
            if (x.Distractors.Count == 0) add($"{p}.distractors", "invalid_fill_blank", "A atividade precisa ter pelo menos um distrator.");
            var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < x.Distractors.Count; j++)
            {
                var value = x.Distractors[j];
                if (string.IsNullOrWhiteSpace(value) || value.Length > 300) add($"{p}.distractors[{j}]", "invalid_fill_blank", "Distrator obrigatório, limite de 300 caracteres.");
                else if (!distinct.Add(value.Trim())) add($"{p}.distractors[{j}]", "invalid_fill_blank", "Distratores repetidos não são permitidos.");
            }
        }
    }
    private static void CheckOrdering(ConceptExchange c, string path, Action<string,string,string> add)
    {
        if (c.OrderingActivities is null) { add($"{path}.orderingActivities", "required", "A lista de ordenação é obrigatória."); return; }
        for (var i = 0; i < c.OrderingActivities.Count; i++)
        {
            var x = c.OrderingActivities[i];
            var p = $"{path}.orderingActivities[{i}]";
            if (x is null) { add(p, "invalid_ordering_activity", "Atividade nula."); continue; }
            if (string.IsNullOrWhiteSpace(x.Instruction) || x.Instruction.Length > 1000 || x.Items is null || x.Items.Count < 2)
            { add(p, "invalid_ordering_activity", "Instrução obrigatória e pelo menos dois itens."); continue; }
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < x.Items.Count; j++)
                if (string.IsNullOrWhiteSpace(x.Items[j])) add($"{p}.items[{j}]", "invalid_ordering_activity", "O item não pode ser vazio.");
                else if (!unique.Add(x.Items[j].Trim())) add($"{p}.items[{j}]", "invalid_ordering_activity", "Os itens devem ser distintos.");
        }
    }

    private static Dictionary<string,string[]> Arrays(Dictionary<string,List<string>> source) =>
        source.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    private static ApiException ImportError(string code, string message, IReadOnlyDictionary<string,string[]>? errors = null) =>
        new((int)HttpStatusCode.BadRequest, code, message, errors);
    private static ApiException NotFound() => new((int)HttpStatusCode.NotFound, "module_not_found", "O módulo não foi encontrado.");
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private IQueryable<Module> ContentGraph(Guid teacherId, Guid moduleId) =>
        db.Modules.Where(m => m.TeacherId == teacherId && m.Id == moduleId)
            .Include(m => m.Concepts).ThenInclude(c => c.Keywords)
            .Include(m => m.Concepts).ThenInclude(c => c.Clues)
            .Include(m => m.Concepts).ThenInclude(c => c.Prerequisites)
            .Include(m => m.Concepts).ThenInclude(c => c.RecognitionActivities)
            .Include(m => m.Concepts).ThenInclude(c => c.FillBlankActivities).ThenInclude(a => a.Answers)
            .Include(m => m.Concepts).ThenInclude(c => c.FillBlankActivities).ThenInclude(a => a.Distractors)
            .Include(m => m.Concepts).ThenInclude(c => c.OrderingActivities).ThenInclude(a => a.Items);

    private static Dictionary<Guid,string> CreateExternalIds(IEnumerable<Concept> concepts)
    {
        var ordered = concepts.OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id).ToArray();
        var reserved = ordered.Where(c => !string.IsNullOrWhiteSpace(c.ExternalId))
            .Select(c => c.ExternalId!).ToHashSet(StringComparer.Ordinal);
        var used = new HashSet<string>(["module"], StringComparer.Ordinal);
        var result = new Dictionary<Guid,string>();
        foreach (var concept in ordered)
        {
            var baseId = string.IsNullOrWhiteSpace(concept.ExternalId) ? Slug(concept.Name) : concept.ExternalId;
            if (string.IsNullOrWhiteSpace(baseId)) baseId = "concept";
            var external = baseId;
            var suffix = 2;
            while (used.Contains(external) || (external != concept.ExternalId && reserved.Contains(external)))
                external = $"{baseId}-{suffix++}";
            used.Add(external);
            result.Add(concept.Id, external);
        }
        return result;
    }    private static string Slug(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var ascii = new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(ascii.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
    }
    private static string SafeFileName(string title)
    {
        var slug = Slug(title);
        return string.IsNullOrEmpty(slug) ? "module" : slug;
    }
}
