# Altinn Notifications - AI agent instructions

Read [README.md](README.md), [getting-started.md](getting-started.md), [.github/copilot-instructions.md](.github/copilot-instructions.md) and the README of each component you change before you start. They, and [.github/workflows/build-and-analyze.yml](.github/workflows/build-and-analyze.yml), which CI runs on every pull request, are the record of how to build and test. This file adds what is easy to miss.

## Layout

| Folder | What it is | Solution |
| :--- | :--- | :--- |
| `components/api` | The Notifications API: `Altinn.Notifications` (web), `.Core`, `.Integrations`, `.Persistence` (PostgreSQL, Yuniql migrations) | `components/api/Altinn.Notifications.API.slnx` |
| `components/email-service` | Sends email through Azure Communication Services | `components/email-service/Altinn.Notifications.Email.slnx` |
| `components/sms-service` | Sends SMS through LinkMobility | `components/sms-service/Altinn.Notifications.Sms.slnx` |
| `components/shared` | Code the three services share | `components/shared/Altinn.Notifications.Shared.slnx` |
| `tools` | The local Aspire stack, its mocks and test harness, and maintenance tools | `tools/Altinn.Notifications.Tools.slnx` |

`Altinn.Notifications.slnx` in the root has all of them. Package versions are in `Directory.Packages.props` (central package management), so a `PackageReference` has no `Version`.

## Build and test

Build each solution you change as CI does, and check that it still loads:

```bash
dotnet build <solution> -c Release
dotnet sln <solution> list
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, `Nullable` and `GenerateDocumentationFile`, so a warning fails the build, and every public type and member needs XML documentation (SA1600 and SA1601 are errors).

Each component has three kinds of tests. Run the ones for the components you change, and all of them when you change `components/shared` or `Directory.Packages.props`:

- `*.Tests`: unit tests, no dependencies.
- `*.IntegrationTests`: need PostgreSQL on `localhost:5432` with the database `notificationsdb` and the roles from [Database Setup](getting-started.md#3-database-setup-postgresql). The API's integration tests and a running API share that database, so stop the API first.
- `*.IntegrationTestsASB`: start PostgreSQL, SQL Server and the Service Bus emulator with Testcontainers, which needs Docker or Podman. They take several minutes to start.

The tools tests are in `tools/test/Altinn.Notifications.Tools.Tests`.

## Code conventions

- Follow [.github/copilot-instructions.md](.github/copilot-instructions.md): braces on every control flow statement, no double spaces, and plain xUnit assertions (`Assert.Equal`, `Assert.Throws`). No FluentAssertions in new tests. Moq is used for mocks.
- StyleCop rules are in `stylecop.json` and `.editorconfig`: using directives outside the namespace, system directives first, `_camelCase` private fields. Fix a warning instead of suppressing it.
- Code, comments, documentation, branch names and commit messages are in English.

## Database migrations

The API creates and updates the database with Yuniql when it starts, from `components/api/src/Altinn.Notifications.Persistence/Migration`. See its `ReadMe.txt`.

- Each function and procedure has its own file in `Migration/FunctionsAndProcedures`. After a build, `DbTools` writes all of them into `01-functions-and-procedures.sql` in the newest `vX.XX` folder.
- When you change a function or procedure after the last deploy, make a new `vX.XX` folder first. Otherwise Yuniql does not run the change.
- When you have not changed one, the build still writes that file, and it shows up as new or changed in `git status`. Do not commit it.

## Run the whole stack locally

`tools/aspire` runs the API, the email and SMS services, PostgreSQL, the Service Bus emulator and local mocks for everything outside the stack (Altinn platform, Maskinporten, ACS, LinkMobility, the token issuer) from one command. See [tools/aspire/README.md](tools/aspire/README.md) for what it starts and every setting, and [tools/aspire/how-to.md](tools/aspire/how-to.md) for recipes. It needs the Aspire CLI 17 or later.

For an agent, run it in the background and stop it when you are done:

```bash
cd tools/aspire
aspire start -lp http                                        # dashboard on http://localhost:15193
aspire wait api                                              # until it is healthy
aspire describe --format json                                # resources, states, URLs and commands
aspire logs sms -n 50
aspire resource testharness-controlpanel send-test-sms       # any dashboard command, with its defaults
aspire resource sms rebuild                                  # picks up a code change; restart does not
aspire stop
```

- **Ports are fixed** (5090-5096 and the mocks' ports), so only one app host can run on the machine. Check with `aspire ps` before you start one.
- **Containers outlive the app host.** `aspire stop` leaves `notifications-db` and the Service Bus emulator running. Stop the containers you started yourself, and never stop or reuse another project's containers. Settings such as another database container or port go in the git-ignored `tools/aspire/appsettings.Local.json`.
- **Credentials are generated, not committed.** The app host generates the token issuer's password, the ACS access key and the LinkMobility delivery report password the first time it runs from a checkout, and keeps them with `aspire secret` for that app host path. Get a token with `curl -u "mock:$(aspire secret get Parameters:mock-auth-password)" "http://localhost:5094/api/GetEnterpriseToken?org=ttd&scopes=altinn:serviceowner/notifications.create"`.
- **Bruno and k6** get those credentials from files the app host writes on every start: `components/api/test/bruno/.env.v2 aspire` and `components/api/test/k6/.secrets.aspire`. Both are git-ignored. Run k6 with `--secret-source=file=.secrets.aspire -e altinn_env=local`, and the Bruno CLI with `--env-file ".env.v2 aspire" --env "v2 aspire"`. Only some Bruno apps read `.env.<environment>` files by themselves.
- **Nothing real is sent.** Email and SMS end in the mocks, where the recipient picks the outcome (`+4799999901` is undelivered, `bounce@example.com` bounces). The control panel's Phone and Inbox tabs show what was sent.

## CI and SonarCloud

`build-and-analyze.yml` ("`.NET Analysis`") builds every changed solution, runs its tests with coverage and sends the results to SonarCloud, project `Altinn_altinn-notifications`.

- It runs on pull requests to `main`, and on pull requests in a stack, for `opened`, `synchronize` and `reopened`. Marking a draft ready for review or adding the PR to a stack does not start it: push a commit, an empty one if needed.
- It ignores changes to `**/*.md`, `docs/**`, `components/api/test/k6/**` and `components/api/test/bruno/**`, so a pull request with only those gets no build and no SonarCloud analysis.

### Quality gate

The gate fails a pull request when the new code has a reliability, security or maintainability rating worse than A, **branch coverage under 80 %**, or more than 5 duplicated lines. Coverage counts branches, not lines: a `??` fallback, a ternary or a validation lambda in `Program.cs` is a branch that needs a test of both outcomes. Move such logic into a method that can be tested without starting a host, and test the throwing paths too.

Check a pull request's result without logging in:

```bash
# The gate and each condition
curl -s "https://sonarcloud.io/api/qualitygates/project_status?projectKey=Altinn_altinn-notifications&pullRequest=<number>"
# The open issues
curl -s "https://sonarcloud.io/api/issues/search?componentKeys=Altinn_altinn-notifications&pullRequest=<number>&resolved=false&ps=100"
# Uncovered branches in new code, per file
curl -s "https://sonarcloud.io/api/measures/component_tree?component=Altinn_altinn-notifications&pullRequest=<number>&metricKeys=new_uncovered_conditions,new_conditions_to_cover&strategy=leaves&ps=500" \
  | jq '.components[] | {path, m: (.measures | map({(.metric): .periods[0].value}) | add)} | select((.m.new_uncovered_conditions // "0") != "0")'
```

Marking an issue as accepted or a false positive needs the "Administer Issues" permission in SonarCloud, which agents do not have. Fix the code, or say in the pull request which issue needs a decision.

### Run the C# rules locally

SonarCloud's C# rules are the `SonarAnalyzer.CSharp` analyzer. Add it to a build without changing the repository, with a targets file outside it, e.g. `sonar.targets` in your temp folder (use the [latest version](https://www.nuget.org/packages/SonarAnalyzer.CSharp)):

```xml
<Project>
  <ItemGroup>
    <PackageReference Include="SonarAnalyzer.CSharp" VersionOverride="10.36.0.15337" PrivateAssets="all" IncludeAssets="runtime;build;native;contentfiles;analyzers;buildtransitive" />
  </ItemGroup>
</Project>
```

```bash
dotnet build <solution> -c Release --no-incremental -p:TreatWarningsAsErrors=false -p:CustomBeforeMicrosoftCommonTargets=<full path to sonar.targets> \
  | grep -oE '[^ ]+\.cs\([0-9]+,[0-9]+\): warning S[0-9]+[^[]*' | sort -u
```

- `VersionOverride` is needed because of central package management, and `TreatWarningsAsErrors=false` lets the build list every finding instead of stopping at the first. `--no-incremental` makes the analyzers run again.
- The existing code has findings too, so compare with a run on `main`, or filter on the files you changed.
- It only covers C#. SonarCloud also checks JavaScript, CSS and HTML (for example in `tools/src/Altinn.Notifications.Tools.TestHarness/wwwroot`), looks for credentials in every file, including `appsettings.json`, and measures coverage and duplication. Check those with the API above once CI has run.

### Findings that come up often

| Rule | Fix |
| :--- | :--- |
| S2068 hard-coded credential, also a `"Password"` value in `appsettings.json` | Leave the setting empty in committed files and validate it at start (`AddOptions<T>().Bind(...).Validate(...).ValidateOnStart()`). In the Aspire stack, generate it with `AddParameter(name, new GenerateParameterDefault { ... }, secret: true, persist: true)`. |
| S1075 hard-coded URI or path delimiter | Build it, e.g. with `UriBuilder`, or read it from settings |
| S6667 logging in a `catch` without the exception, S2139 log and rethrow | Pass the exception to the logger, or log after the `catch` |
| S107 more than 7 parameters | Group them in a record, e.g. with `[AsParameters]` in a minimal API |
| S1192 the same string literal several times | A `const` |
| S1144 unused private setter | Remove it, or use `init` |
| S3776 cognitive complexity over 15 | Split the method |
| CA1861 a constant array as an argument | A `static readonly` field |
| SYSLIB1045 `new Regex(...)` | `[GeneratedRegex]` in a `partial` class |
| S9422 `lock` on an `object` | `System.Threading.Lock` |

## Pull requests

- Branch from the current `origin/main`. Pull requests are squash merged, so the title becomes the commit message on `main`: one sentence that says what the change does.
- Include tests, and show in the description how the change was verified.
- **Stacked pull requests**: each is based on the branch of the one below, and they are merged bottom-up. After a squash merge, merge `main` into the next pull request before it is merged. GitHub can group them as a stack, bottom first, where each pull request's base is the head branch of the one below:

  ```bash
  gh api -X POST repos/Altinn/altinn-notifications/stacks --input - <<< '{"pull_requests":[<bottom>, <next>, <top>]}'
  ```

- **Before merging a pull request whose CI ran a while ago**, check what has reached `main` since. A change to an interface on `main` breaks a pull request that is green on its own, and it only shows up after the merge.

## Do not commit

- A regenerated `01-functions-and-procedures.sql` when you have not changed a function or procedure. Git does not ignore it.
- Credentials of any kind, also for test environments. Use user secrets, or generated Aspire parameters in the local stack.

Git already ignores `appsettings.Local.json`, the Bruno `.env` files and the k6 `.secrets` files, so do not force-add them.
