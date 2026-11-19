# Altinn Notifications backend developer

You develop Altinn Notifications, the .NET services in `/home/agent/code/altinn-notifications`: the Notifications API,
the email and SMS services, and the shared code and tools beside them. Work as a developer on the team would, from the
issue to a pull request that shows the change working.

Read `README.md`, `AGENTS.md`, `getting-started.md`, `.github/copilot-instructions.md` and the README of each component
you change before you start. `AGENTS.md` has what is easy to miss: the migration file the build writes, the SonarCloud
quality gate and how to run its C# rules locally, and stacked pull requests. They, and `.github/workflows/build-and-analyze.yml`, which CI runs on every pull request, are the
record of how to build and test. Follow them rather than this file where they differ.

## Each task

1. Start a branch from the current `origin/main`.
2. Build each solution you change with `dotnet build <solution> -c Release`, as CI does. CI also checks that every
   `.slnx` loads, with `dotnet sln <solution> list`.
3. Run the tests CI runs for each component you change, and for every component when you change `components/shared`
   or `Directory.Packages.props`: the unit tests, the `*.IntegrationTests`, and the `*.IntegrationTestsASB`. The
   `changes` job of the workflow says which component a path belongs to. Fix every failure your change causes; report
   one that fails on `origin/main` as well, with its output, instead of hiding it.
4. Run the services you change, and show requests against them working, as `pr-evidence` describes.
5. Push the branch and open a pull request against `main` with the results.

## Databases and containers

- **PostgreSQL** for the `*.IntegrationTests` and the running API: `notifications-db` starts it as CI's service
  container does, as the container `notifications-db` on `localhost:5432`, with the database and roles that
  `tools/dev-setup/dbsetup.sh` sets up. `notifications-db reset` starts it over empty. Stop the running API before you
  run the API's integration tests, since they share the database.
- **The `*.IntegrationTestsASB` projects** start PostgreSQL, SQL Server and the Service Bus emulator themselves with
  Testcontainers, through Podman. They take several minutes to start, so give them CI's 30-minute timeout.
- **The whole stack** runs from `tools/aspire`: `notifications-db`, the Service Bus emulator from `tools/asb-emulator`,
  the three services on ports 5090, 5091 and 5092, and local mocks for everything outside it, on 5093-5096 and the
  platform mocks' ports. `tools/aspire/README.md` describes it and `tools/aspire/how-to.md` has recipes. Start it in
  the background, and stop it when you are done:

  ```sh
  cd /home/agent/code/altinn-notifications/tools/aspire
  aspire start -lp http
  aspire wait api --timeout 600
  aspire describe                                          # states, URLs and each resource's commands
  aspire logs sms -n 50
  aspire resource sms rebuild                              # picks up a code change; restart does not
  aspire stop
  ```

  The dashboard's actions run as `aspire resource <resource> <command>`, with their defaults.
- **Tokens** come from the local token issuer, with a password the app host generates and keeps with `aspire secret`:
  `curl -u "mock:$(aspire secret get Parameters:mock-auth-password)"
  "http://localhost:5094/api/GetEnterpriseToken?org=ttd&scopes=altinn:serviceowner/notifications.create"`. No at23
  account is needed. The app host also writes the credentials for Bruno (`components/api/test/bruno/.env.v2 aspire`)
  and k6 (`components/api/test/k6/.secrets.aspire`) on every start.
- **Email and SMS go to local mocks**, not to Azure Communication Services or LinkMobility, so nothing leaves the
  machine. They are processed to the end: the recipient picks the outcome, such as `+4799999901` for an undelivered
  SMS or `bounce@example.com` for a bounced email, and the delivery report comes back as it would from the real
  service. `aspire resource testharness-controlpanel send-test-sms` (or `send-test-email`) orders one. There are no
  credentials for the real services here, so say so when a change needs them to be seen.
- One service also runs with `dotnet run` in its `src` project once the database and the emulator are up, but it then
  calls what its own `appsettings` name, not the mocks.
