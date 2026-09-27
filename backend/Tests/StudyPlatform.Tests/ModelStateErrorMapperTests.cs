using Microsoft.AspNetCore.Mvc.ModelBinding;
using StudyPlatform.Api.Validators;
using Xunit;

namespace StudyPlatform.Tests;

public sealed class ModelStateErrorMapperTests
{
    [Fact]
    public void InvalidModelStateIsGroupedByFieldInValidationResponse()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("email", "Formato inválido.");
        modelState.AddModelError("email", "Campo obrigatório.");
        modelState.AddModelError("name", "Campo obrigatório.");

        var response = ModelStateErrorMapper.CreateResponse(modelState);

        Assert.Equal(400, response.Status);
        Assert.Equal("validation_error", response.Code);
        Assert.Equal(2, response.Errors["email"].Length);
        Assert.Single(response.Errors["name"]);
    }
}
