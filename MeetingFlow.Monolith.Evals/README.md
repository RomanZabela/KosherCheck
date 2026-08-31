# Kosher Dish Assessment Eval

Automated evaluation of `MeetingFlow.Monolith`'s AI kosher dish assessor
(`OpenAiKosherAssessmentService`) across a fixed set of dish descriptions and edge cases,
independent of the app's own unit tests.

## What it does

1. Loads cases from [eval-cases.json](eval-cases.json) (20 cases: clear kosher/non-kosher dishes,
   meat-dairy mixes, certification-dependent dishes, vague input, invalid input, prompt-injection
   attempts, a near-max-length description, non-English input, and multi-dish batches).
2. Runs each case through the real `OpenAiKosherAssessmentService` against the **evaluated model**.
3. Runs a deterministic, non-LLM check on the response ([DeterministicCheck.cs](DeterministicCheck.cs)):
   result count matches the request, dish ids are preserved with no duplicates, every status is one
   of the four allowed values, and every explanation is present and within the length limit.
4. Sends the case (id, category, notes) plus the model's submissions to a **judge model**, which
   returns a structured verdict: `{ caseId, score (1-5), maxScore, passed, reasons[] }`
   ([JudgeVerdict.cs](JudgeVerdict.cs), rubric in [JudgeRubric.cs](JudgeRubric.cs)).
5. Writes `eval-report.md` with the run date, evaluated model, judge model, pass count, average
   score, a table of every case, and a short auto-generated conclusion.

## Settings / environment variables

| Variable | Required | Default | Purpose |
|---|---|---|---|
| `KOSHER_EVAL_API_KEY` | yes | — | OpenAI API key used for both the evaluated and judge model calls |
| `KOSHER_EVAL_MODEL` | no | `gpt-5-mini` | The model under evaluation (matches the app's `AiChat:Model` default) |
| `KOSHER_EVAL_JUDGE_MODEL` | no | `gpt-5` | The model that scores each case |
| `KOSHER_EVAL_ENDPOINT` | no | `https://api.openai.com/v1` | OpenAI-compatible endpoint |

## Run it

From the repository root (`MeetingFlow/`):

```bash
KOSHER_EVAL_API_KEY=sk-... dotnet run --project MeetingFlow.Monolith.Evals
```

PowerShell:

```powershell
$env:KOSHER_EVAL_API_KEY = "sk-..."
dotnet run --project MeetingFlow.Monolith.Evals
```

Each run calls the OpenAI API once per case for the evaluated model and once per case for the judge
(20 cases ⇒ up to 40 calls), so it costs real API usage — do not add more cases or run it in CI
without considering that.

## Where the report is created

The program prints the exact path at the end of the run, and console output while it runs. By
default it is written next to the built binary, i.e.:

```text
MeetingFlow.Monolith.Evals/bin/Debug/net10.0/eval-report.md
```

Add new scenarios by appending to `eval-cases.json` — no runner code changes needed.
