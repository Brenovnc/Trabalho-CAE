using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyPlatform.Api.DTOs.Auth;
using StudyPlatform.Api.DTOs.Concepts;
using StudyPlatform.Api.DTOs.Modules;
using StudyPlatform.Api.Services.Auth;
using StudyPlatform.Api.Services.Modules;

namespace StudyPlatform.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.Teacher)]
[Route("api/modules")]
public sealed class ModulesController(ModuleService modules) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ModuleSummaryResponse>>> List(CancellationToken ct) =>
        Ok(await modules.ListAsync(TeacherId, ct));

    [HttpPost]
    public async Task<ActionResult<ModuleDetailsResponse>> Create(CreateModuleRequest request, CancellationToken ct)
    {
        var module = await modules.CreateAsync(TeacherId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = module.Id }, module);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ModuleDetailsResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await modules.GetAsync(TeacherId, id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ModuleDetailsResponse>> Update(Guid id, UpdateModuleRequest request, CancellationToken ct) =>
        Ok(await modules.UpdateAsync(TeacherId, id, request, ct));

    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<ModuleDetailsResponse>> Duplicate(Guid id, CancellationToken ct)
    {
        var copy = await modules.DuplicateAsync(TeacherId, id, ct);
        return CreatedAtAction(nameof(Get), new { id = copy.Id }, copy);
    }

    [HttpGet("{id:guid}/publication-validation")]
    public async Task<ActionResult<PublicationValidationResponse>> ValidatePublication(Guid id, CancellationToken ct) =>
        Ok(await modules.ValidatePublicationAsync(TeacherId, id, ct));

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<ModuleDetailsResponse>> Publish(Guid id, CancellationToken ct)
    {
        var validation = await modules.ValidatePublicationAsync(TeacherId, id, ct);
        if (!validation.IsValid) return UnprocessableEntity(validation);
        return Ok(await modules.PublishAsync(TeacherId, id, ct));
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await modules.ArchiveAsync(TeacherId, id, ct);
        return NoContent();
    }

    [HttpGet("{moduleId:guid}/concepts")]
    public async Task<ActionResult<IReadOnlyList<ConceptSummaryResponse>>> ListConcepts(Guid moduleId, CancellationToken ct) =>
        Ok(await modules.ListConceptsAsync(TeacherId, moduleId, ct));

    [HttpPost("{moduleId:guid}/concepts")]
    public async Task<ActionResult<ConceptDetailsResponse>> CreateConcept(Guid moduleId, SaveConceptRequest request, CancellationToken ct)
    {
        var concept = await modules.CreateConceptAsync(TeacherId, moduleId, request, ct);
        return CreatedAtAction(nameof(GetConcept), new { moduleId, conceptId = concept.Id }, concept);
    }

    [HttpGet("{moduleId:guid}/concepts/{conceptId:guid}")]
    public async Task<ActionResult<ConceptDetailsResponse>> GetConcept(Guid moduleId, Guid conceptId, CancellationToken ct) =>
        Ok(await modules.GetConceptAsync(TeacherId, moduleId, conceptId, ct));

    [HttpPut("{moduleId:guid}/concepts/{conceptId:guid}")]
    public async Task<ActionResult<ConceptDetailsResponse>> UpdateConcept(Guid moduleId, Guid conceptId, SaveConceptRequest request, CancellationToken ct) =>
        Ok(await modules.UpdateConceptAsync(TeacherId, moduleId, conceptId, request, ct));

    [HttpPost("{moduleId:guid}/concepts/{conceptId:guid}/duplicate")]
    public async Task<ActionResult<ConceptDetailsResponse>> DuplicateConcept(Guid moduleId, Guid conceptId, CancellationToken ct)
    {
        var copy = await modules.DuplicateConceptAsync(TeacherId, moduleId, conceptId, ct);
        return CreatedAtAction(nameof(GetConcept), new { moduleId, conceptId = copy.Id }, copy);
    }

    [HttpPost("{moduleId:guid}/concepts/{conceptId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateConcept(Guid moduleId, Guid conceptId, CancellationToken ct)
    {
        await modules.DeactivateConceptAsync(TeacherId, moduleId, conceptId, ct);
        return NoContent();
    }

    private Guid TeacherId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

