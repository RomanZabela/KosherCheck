namespace MeetingFlow.Monolith.Services;

public sealed record KosherInputValidation(
    bool IsValid,
    IReadOnlyList<string> Dishes,
    IReadOnlyList<string> Errors);

public static class KosherInputValidator
{
    public const int MinimumDishCount = 1;
    public const int MaximumDishCount = 10;
    public const int MaximumDishLength = 500;

    public static KosherInputValidation Validate(IReadOnlyList<string>? dishes)
    {
        var suppliedDishes = dishes ?? [];
        var trimmedDishes = suppliedDishes.Select(dish => dish?.Trim() ?? string.Empty).ToList();
        var errors = new List<string>();

        if (trimmedDishes.Count is < MinimumDishCount or > MaximumDishCount)
        {
            errors.Add("Enter between 1 and 10 dishes.");
        }

        for (var index = 0; index < trimmedDishes.Count; index++)
        {
            var dish = trimmedDishes[index];
            if (string.IsNullOrWhiteSpace(dish))
            {
                errors.Add($"Dish {index + 1} cannot be empty.");
            }
            else if (dish.Length > MaximumDishLength)
            {
                errors.Add($"Dish {index + 1} must be 500 characters or fewer.");
            }
        }

        return new KosherInputValidation(errors.Count == 0, trimmedDishes, errors);
    }
}
