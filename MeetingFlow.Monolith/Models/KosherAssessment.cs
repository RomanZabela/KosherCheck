using System.ComponentModel;

namespace MeetingFlow.Monolith.Models;

public sealed record DishCheckEntry(string Id, string Description);

public sealed class DishAssessmentBatch
{
    [Description("One assessment for every dish identifier supplied by the application.")]
    public required List<DishAssessmentItem> Items { get; init; }
}

public sealed class DishAssessmentItem
{
    [Description("The exact dish identifier supplied by the application, such as dish-1.")]
    public required string DishId { get; init; }

    [Description("KOSHER, NOT_KOSHER, CONDITIONAL, or INVALID_INPUT.")]
    public required DishAssessmentStatus Status { get; init; }

    [Description("A concise English explanation grounded in the described ingredients and preparation conditions.")]
    public required string Explanation { get; init; }
}

public enum DishAssessmentStatus
{
    [Description("The description provides enough information to classify the dish as kosher.")]
    Kosher,

    [Description("The description clearly contains a non-kosher ingredient or combination.")]
    NotKosher,

    [Description("The classification depends on missing details such as certification, ingredients, equipment, or preparation.")]
    Conditional,

    [Description("Use only when the supplied text is clearly not a food or dish description.")]
    InvalidInput
}
