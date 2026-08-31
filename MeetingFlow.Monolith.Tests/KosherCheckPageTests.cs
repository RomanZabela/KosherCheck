using System.Text.Json;
using MeetingFlow.Monolith.Models;
using MeetingFlow.Monolith.Pages;
using MeetingFlow.Monolith.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MeetingFlow.Monolith.Tests;

public class KosherCheckPageTests
{
    [Fact]
    public void TrafficControl_AppliesOnlyToKosherCheckPosts()
    {
        var checkPost = new DefaultHttpContext();
        checkPost.Request.Method = HttpMethods.Post;
        checkPost.Request.Path = "/KosherCheck";
        var checkGet = new DefaultHttpContext();
        checkGet.Request.Method = HttpMethods.Get;
        checkGet.Request.Path = "/KosherCheck";
        var checkPostWithTrailingSlash = new DefaultHttpContext();
        checkPostWithTrailingSlash.Request.Method = HttpMethods.Post;
        checkPostWithTrailingSlash.Request.Path = "/KosherCheck/";
        var otherPost = new DefaultHttpContext();
        otherPost.Request.Method = HttpMethods.Post;
        otherPost.Request.Path = "/Registrations/Create";

        Assert.True(KosherCheckTrafficControl.AppliesTo(checkPost));
        Assert.True(KosherCheckTrafficControl.AppliesTo(checkPostWithTrailingSlash));
        Assert.False(KosherCheckTrafficControl.AppliesTo(checkGet));
        Assert.False(KosherCheckTrafficControl.AppliesTo(otherPost));
    }

    [Fact]
    public async Task OnPostCheckAsync_ReturnsOriginalDishTextAndAssessment()
    {
        var service = new RecordingAssessmentService(new DishAssessmentBatch
        {
            Items =
            [
                new DishAssessmentItem
                {
                    DishId = "dish-1",
                    Status = DishAssessmentStatus.Conditional,
                    Explanation = "Certification details are missing."
                }
            ]
        });
        var page = CreatePage(service);
        page.Dishes = ["  Plant-based cheeseburger  "];

        var action = await page.OnPostCheckAsync(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(action);
        Assert.Equal(200, json.StatusCode);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(json.Value));
        var item = document.RootElement.GetProperty("results")[0];
        Assert.Equal("Plant-based cheeseburger", item.GetProperty("dish").GetString());
        Assert.Equal("CONDITIONAL", item.GetProperty("status").GetString());
        Assert.Equal("dish-1", service.ReceivedDishes.Single().Id);
    }

    [Fact]
    public async Task OnPostCheckAsync_ReturnsValidationErrorsWithoutCallingAi()
    {
        var service = new RecordingAssessmentService(null);
        var page = CreatePage(service);
        page.Dishes = [" "];

        var action = await page.OnPostCheckAsync(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(action);
        Assert.Equal(400, json.StatusCode);
        Assert.Empty(service.ReceivedDishes);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(json.Value));
        Assert.Equal(
            "Dish 1 cannot be empty.",
            document.RootElement.GetProperty("errors")[0].GetString());
    }

    [Fact]
    public async Task OnPostCheckAsync_ReturnsGenericUnavailableMessageWhenAiFails()
    {
        var service = new ThrowingAssessmentService();
        var page = CreatePage(service);
        page.Dishes = ["Falafel"];

        var action = await page.OnPostCheckAsync(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(action);
        Assert.Equal(503, json.StatusCode);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(json.Value));
        Assert.Equal(
            "Kosher checking is currently unavailable. Please try again later.",
            document.RootElement.GetProperty("error").GetString());
    }

    private static KosherCheckModel CreatePage(IKosherAssessmentService service) =>
        new(service, NullLogger<KosherCheckModel>.Instance);

    private sealed class RecordingAssessmentService(DishAssessmentBatch? result)
        : IKosherAssessmentService
    {
        public IReadOnlyList<DishCheckEntry> ReceivedDishes { get; private set; } = [];

        public Task<DishAssessmentBatch> AssessAsync(
            IReadOnlyList<DishCheckEntry> dishes,
            CancellationToken cancellationToken = default)
        {
            ReceivedDishes = dishes;
            return Task.FromResult(result ?? new DishAssessmentBatch { Items = [] });
        }
    }

    private sealed class ThrowingAssessmentService : IKosherAssessmentService
    {
        public Task<DishAssessmentBatch> AssessAsync(
            IReadOnlyList<DishCheckEntry> dishes,
            CancellationToken cancellationToken = default) =>
            throw new KosherAssessmentException("Provider details must not reach the browser.");
    }
}
