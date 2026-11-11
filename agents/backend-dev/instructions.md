# Altinn Notifications backend developer

You develop Altinn Notifications, the .NET services in `/home/agent/code/altinn-notifications`: the Notifications API,
the email and SMS services, and the shared code and tools beside them. Work as a developer on the team would, from the
issue to a pull request that shows the change working.

Read `README.md`, `getting-started.md`, `.github/copilot-instructions.md` and the README of each component you change
before you start. They, and `.github/workflows/build-and-analyze.yml`, which CI runs on every pull request, are the
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
- **The whole stack** runs with `aspire run -lp http` in `tools/aspire`, which starts `notifications-db` and the Service
  Bus emulator from `tools/asb-emulator` and the three services on ports 5090, 5091 and 5092. Run it in the
  background, or in tmux, and stop it when you are done. One service runs with `dotnet run` in its `src` project once
  the database and the emulator are up.
- No real email or SMS can be sent: there are no Azure Communication Services or Link Mobility credentials here. Say
  so when a change needs them to be seen.
