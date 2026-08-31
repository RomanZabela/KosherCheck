using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using MeetingFlow.Monolith.Evals;
using MeetingFlow.Monolith.Models;
using MeetingFlow.Monolith.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;

// appsettings.Local.json is optional and lets you set these without exporting shell
// environment variables. A real environment variable of the same name always wins.
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var apiKey = configuration["KOSHER_EVAL_API_KEY"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine(
        "KOSHER_EVAL_API_KEY is not set. Set it as an environment variable, or add it to " +
        "appsettings.Local.json next to this project. See README.md.");
    return 1;
}

var evaluatedModel = configuration["KOSHER_EVAL_MODEL"] ?? "gpt-5-mini";
var judgeModel = configuration["KOSHER_EVAL_JUDGE_MODEL"] ?? "gpt-5";
var endpoint = configuration["KOSHER_EVAL_ENDPOINT"] ?? "https://api.openai.com/v1";

var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false) }
};

var casesPath = Path.Combine(AppContext.BaseDirectory, "eval-cases.json");
var casesJson = await File.ReadAllTextAsync(casesPath);
var cases = JsonSerializer.Deserialize<List<EvalCase>>(casesJson, serializerOptions)
    ?? throw new InvalidOperationException($"Could not load evaluation cases from {casesPath}.");

Console.WriteLine($"Loaded {cases.Count} case(s) from {casesPath}");
Console.WriteLine($"Evaluated model: {evaluatedModel}");
Console.WriteLine($"Judge model: {judgeModel}");
Console.WriteLine();

var openAiClient = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

var evaluatedChatClient = openAiClient.GetChatClient(evaluatedModel).AsIChatClient();
var judgeChatClient = openAiClient.GetChatClient(judgeModel).AsIChatClient();

var evaluatedService = new OpenAiKosherAssessmentService(
    evaluatedChatClient,
    NullLogger<OpenAiKosherAssessmentService>.Instance,
    new SemaphoreSlim(4, 4));

var outcomes = new List<CaseOutcome>();

foreach (var evalCase in cases)
{
    Console.WriteLine($"Running case: {evalCase.Id}");
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

    var entries = evalCase.Dishes
        .Select((dish, index) => new DishCheckEntry($"dish-{index + 1}", dish))
        .ToList();

    DishAssessmentBatch? batch = null;
    string? systemError = null;
    try
    {
        batch = await evaluatedService.AssessAsync(entries, cts.Token);
    }
    catch (Exception exception)
    {
        systemError = exception.Message;
        Console.Error.WriteLine($"  [debug] {exception.GetType().Name}: {exception.Message}");
        if (exception.InnerException is not null)
        {
            Console.Error.WriteLine($"  [debug] inner: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}");
        }
    }

    bool deterministicPassed;
    string deterministicNote;
    List<DishSubmission> submissions;

    if (batch is null)
    {
        deterministicPassed = false;
        deterministicNote = $"The system under test failed: {systemError}";
        submissions = [];
    }
    else
    {
        (deterministicPassed, deterministicNote) = DeterministicCheck.Evaluate(entries, batch);
        submissions = entries
            .Zip(batch.Items, (entry, item) => new DishSubmission(entry.Description, ToWireStatus(item.Status), item.Explanation))
            .ToList();
    }

    JudgeVerdict verdict;
    if (batch is null)
    {
        verdict = new JudgeVerdict
        {
            CaseId = evalCase.Id,
            Score = 0,
            MaxScore = 5,
            Passed = false,
            Reasons = [$"The system under test raised an error, so there is no response to judge: {systemError}"]
        };
    }
    else
    {
        verdict = await JudgeAsync(judgeChatClient, serializerOptions, evalCase, submissions, cts.Token);
    }

    outcomes.Add(new CaseOutcome(evalCase, deterministicPassed, deterministicNote, submissions, verdict));
    Console.WriteLine($"  deterministic: {(deterministicPassed ? "PASS" : "FAIL")} | judge score: {verdict.Score}/{verdict.MaxScore} | passed: {verdict.Passed}");
}

var report = ReportWriter.Build(outcomes, evaluatedModel, judgeModel, DateTimeOffset.UtcNow);
var reportPath = Path.Combine(AppContext.BaseDirectory, "eval-report.md");
await File.WriteAllTextAsync(reportPath, report);

Console.WriteLine();
Console.WriteLine($"Passing cases: {outcomes.Count(o => o.OverallPassed)} / {outcomes.Count}");
Console.WriteLine($"Report written to: {reportPath}");

return 0;

static async Task<JudgeVerdict> JudgeAsync(
    IChatClient judgeChatClient,
    JsonSerializerOptions serializerOptions,
    EvalCase evalCase,
    List<DishSubmission> submissions,
    CancellationToken cancellationToken)
{
    var payload = new
    {
        caseId = evalCase.Id,
        category = evalCase.Category,
        notes = evalCase.Notes,
        submissions
    };

    var messages = new[]
    {
        new ChatMessage(ChatRole.System, JudgeRubric.Instructions),
        new ChatMessage(ChatRole.User, JsonSerializer.Serialize(payload, serializerOptions))
    };

    try
    {
        var response = await judgeChatClient.GetResponseAsync<JudgeVerdict>(
            messages,
            serializerOptions,
            options: null,
            useJsonSchemaResponseFormat: true,
            cancellationToken);

        if (!response.TryGetResult(out var parsed) || parsed is null)
        {
            return new JudgeVerdict
            {
                CaseId = evalCase.Id,
                Score = 0,
                MaxScore = 5,
                Passed = false,
                Reasons = ["The judge model did not return a schema-valid verdict."]
            };
        }

        return parsed;
    }
    catch (Exception exception)
    {
        return new JudgeVerdict
        {
            CaseId = evalCase.Id,
            Score = 0,
            MaxScore = 5,
            Passed = false,
            Reasons = [$"The judge call failed: {exception.Message}"]
        };
    }
}

static string ToWireStatus(DishAssessmentStatus status) => status switch
{
    DishAssessmentStatus.Kosher => "KOSHER",
    DishAssessmentStatus.NotKosher => "NOT_KOSHER",
    DishAssessmentStatus.Conditional => "CONDITIONAL",
    DishAssessmentStatus.InvalidInput => "INVALID_INPUT",
    _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown kosher assessment status.")
};
