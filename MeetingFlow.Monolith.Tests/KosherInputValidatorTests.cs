using MeetingFlow.Monolith.Services;
using Xunit;

namespace MeetingFlow.Monolith.Tests;

public class KosherInputValidatorTests
{
    [Fact]
    public void Validate_TrimsValidDishesAndPreservesDuplicates()
    {
        var result = KosherInputValidator.Validate(["  Falafel  ", "Falafel"]);

        Assert.True(result.IsValid);
        Assert.Equal(["Falafel", "Falafel"], result.Dishes);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Validate_RejectsDishCountsOutsideAllowedRange(int count)
    {
        var dishes = Enumerable.Range(1, count).Select(index => $"Dish {index}").ToList();

        var result = KosherInputValidator.Validate(dishes);

        Assert.False(result.IsValid);
        Assert.Contains("Enter between 1 and 10 dishes.", result.Errors);
    }

    [Fact]
    public void Validate_RejectsBlankDish()
    {
        var result = KosherInputValidator.Validate(["Falafel", "   "]);

        Assert.False(result.IsValid);
        Assert.Contains("Dish 2 cannot be empty.", result.Errors);
    }

    [Fact]
    public void Validate_RejectsDishLongerThanFiveHundredCharacters()
    {
        var result = KosherInputValidator.Validate([new string('a', 501)]);

        Assert.False(result.IsValid);
        Assert.Contains("Dish 1 must be 500 characters or fewer.", result.Errors);
    }
}
