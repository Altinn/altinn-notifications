# Tools — Conventions

Conventions all tools in this directory must follow. New tools must adhere to these patterns; updates to existing tools should align them over time.

## Project setup

- Target `net10.0`.
- Reference `Altinn.Notifications.Shared` (at `../../components/shared/src/Altinn.Notifications.Shared/`) for shared domain types.
- Set a unique `<UserSecretsId>` in the `.csproj`, e.g. `my-tool-name-<short-guid>`.
- Add an `AssemblyInfo.cs` with `[assembly: InternalsVisibleTo("Altinn.Notifications.Tools.Tests")]` so the shared test project can access internal types.

## Configuration

- **`appsettings.json`** holds defaults that work against the local dev setup (localhost PostgreSQL, local ASB emulator, localhost HTTP endpoints). No secrets in this file.
- **User secrets** override `appsettings.json` for non-localhost environments. Loaded with `builder.Configuration.AddUserSecrets<Program>(optional: true)`.
- Environment variables are also loaded.
- All settings are bound to plain POCO classes via `services.Configure<T>(configuration.GetSection("..."))`.

Secrets always go in user secrets, never in `appsettings.json`:
- PostgreSQL connection string
- Azure Service Bus connection string
- Any endpoint credentials (API keys, HTTP Basic auth username/password)

The tool's `README.md` must list all required `dotnet user-secrets set` commands.

## DI wiring

Wire everything directly in `Program.cs` using `Microsoft.Extensions.Hosting`. No separate `ConfigurationUtil` or `Startup` class:

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();

builder.Services.Configure<MySettings>(builder.Configuration.GetSection("MySettings"));
// register DB, clients, services ...

var host = builder.Build();
using var scope = host.Services.CreateScope();
var menu = scope.ServiceProvider.GetRequiredService<ConsoleMenuService>();
Environment.Exit(await menu.RunMenuAsync());
```

## Interactive menu

- Every tool exposes an interactive console menu via a `ConsoleMenuService`.
- Menu items are numbered; `0` always exits.
- The menu loops until the user chooses exit.
- Ctrl+C exits immediately — do not intercept cancellation signals to avoid hanging at `ReadLine` prompts.

## Two-step query-to-file → file-to-action workflow

For any tool that reads from a data source and acts on those entries, split the work into two menu operations:

1. **Inspect / query**: fetches data, writes results to a local JSON file, prints a summary (count + file path). The operator reviews the file before proceeding.
2. **Act**: reads the file from step 1, performs the action (send, delete, update), and reports per-entry results to console. Entries that fail are left untouched in the source.

Output file paths are configured in `appsettings.json` with sensible defaults (e.g. `sms-send-dlq-sending-expired.json`). Files are overwritten on each inspect run.

This prevents blind bulk operations and makes it safe to re-run a failed act against the same file without re-querying.

## Exit codes

- `0` — all operations completed successfully.
- `1` — one or more entries failed, or an unhandled exception occurred.

## Tests

Tests live in the shared test project at `tools/test/Altinn.Notifications.Tools.Tests/`, in a subdirectory named after the tool (e.g. `DlqManager/`, `RetryDeadDeliveryReports/`).

Two layers of tests are expected:

**Integration tests** (primary) — use `[Collection(nameof(IntegrationContainersCollection))]` to share the `IntegrationContainersFixture`, which spins up real PostgreSQL and ASB emulator containers and applies all migrations. Implement `IAsyncLifetime` for per-test seed/cleanup. Test the full service behaviour including DB state assertions.

**Unit tests** — use Moq for error paths that are impractical to trigger with real infrastructure (e.g. HTTP client returns 500, client throws an exception). Only use mocks where real containers cannot cover the scenario.

Interactive menus are tested by redirecting `Console.In`/`Console.Out`:

```csharp
Console.SetIn(new StringReader("1\n0\n")); // simulate user entering "1" then "0"
var output = new StringWriter();
Console.SetOut(output);
await service.RunMenuAsync();
Assert.Contains("expected text", output.ToString());
```

## README

Every tool must have a `README.md` covering:
- Prerequisites
- `dotnet user-secrets set` commands for all required secrets
- How to run (`dotnet run`)
- A step-by-step walkthrough of each menu operation
