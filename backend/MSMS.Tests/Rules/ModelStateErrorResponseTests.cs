using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using MSMS.Api.Common.Errors;

namespace MSMS.Tests.Rules;

/// <summary>Lỗi do ASP.NET phát hiện trước khi vào code dự án cũng phải theo định dạng { code, message } tiếng Việt (Conventions §3).</summary>
public class ModelStateErrorResponseTests
{
    private static JsonElement Respond(Action<ModelStateDictionary> fill, out int status)
    {
        var modelState = new ModelStateDictionary();
        fill(modelState);
        var result = Assert.IsType<BadRequestObjectResult>(
            ModelStateErrorResponse.Create(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor(), modelState)));
        status = result.StatusCode ?? 0;
        return JsonSerializer.SerializeToElement(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    [Fact]
    public void AMissingRequiredField_ReturnsTheProjectErrorShape_InVietnamese()
    {
        var body = Respond(m => m.AddModelError("Code", "The Code field is required."), out var status);

        Assert.Equal(400, status);
        Assert.Equal(ErrorCodes.ValidationFailed, body.GetProperty("code").GetString());
        Assert.Equal(ModelStateErrorResponse.Message, body.GetProperty("message").GetString());
        Assert.Contains("Kiểm tra lại", body.GetProperty("message").GetString());
    }

    [Fact]
    public void FieldDetails_AreKeptForDebugging_ButNeverReplaceTheMessageTheUiShows()
    {
        var body = Respond(m =>
        {
            m.AddModelError("Name", "The Name field is required.");
            m.AddModelError("GradeLevel", "The value 'x' is not valid.");
        }, out _);

        var errors = body.GetProperty("errors");
        Assert.Equal("The Name field is required.", errors.GetProperty("Name")[0].GetString());
        Assert.Equal("The value 'x' is not valid.", errors.GetProperty("GradeLevel")[0].GetString());
        Assert.DoesNotContain("required", body.GetProperty("message").GetString());
    }

    [Fact]
    public void AMalformedJsonBody_IsHandledTheSameWay()
    {
        var body = Respond(m => m.AddModelError(string.Empty, "A non-empty request body is required."), out var status);

        Assert.Equal(400, status);
        Assert.Equal(ErrorCodes.ValidationFailed, body.GetProperty("code").GetString());
    }
}
