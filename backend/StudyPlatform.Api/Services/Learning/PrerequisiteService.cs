namespace StudyPlatform.Api.Services.Learning;

public sealed record ConceptPrerequisiteNode(Guid Id, IReadOnlyCollection<Guid> PrerequisiteIds);

public sealed record PrerequisiteReferenceIssue(string Code, string Message);
public sealed class PrerequisiteService
{
    public IReadOnlyList<PrerequisiteReferenceIssue> ValidateReferences(
        Guid conceptId, IReadOnlyCollection<Guid> conceptIdsInModule, IReadOnlyCollection<Guid> requestedIds)
    {
        var errors = new List<PrerequisiteReferenceIssue>();
        if (requestedIds.Count != requestedIds.Distinct().Count())
            errors.Add(new("duplicate_prerequisite", "Um pré-requisito foi informado mais de uma vez."));
        if (requestedIds.Contains(conceptId))
            errors.Add(new("self_prerequisite", "Um conceito não pode depender de si próprio."));
        if (requestedIds.Any(id => !conceptIdsInModule.Contains(id)))
            errors.Add(new("invalid_prerequisite", "Todos os pré-requisitos devem existir e pertencer ao mesmo módulo."));
        return errors;
    }
    public string? FindCycle(IEnumerable<ConceptPrerequisiteNode> nodes)
    {
        var graph = nodes.ToDictionary(node => node.Id, node => node.PrerequisiteIds.ToArray());
        var states = new Dictionary<Guid, VisitState>();
        var path = new List<Guid>();

        foreach (var id in graph.Keys)
        {
            var cycle = Visit(id);
            if (cycle is not null) return cycle;
        }

        return null;

        string? Visit(Guid id)
        {
            if (states.TryGetValue(id, out var state))
            {
                if (state == VisitState.Complete) return null;
                var start = path.IndexOf(id);
                var cyclePath = path.Skip(start).Append(id);
                return string.Join(" → ", cyclePath);
            }

            states[id] = VisitState.Visiting;
            path.Add(id);
            foreach (var prerequisiteId in graph[id])
            {
                if (graph.ContainsKey(prerequisiteId))
                {
                    var cycle = Visit(prerequisiteId);
                    if (cycle is not null) return cycle;
                }
            }
            path.RemoveAt(path.Count - 1);
            states[id] = VisitState.Complete;
            return null;
        }
    }

    private enum VisitState { Visiting, Complete }
}



