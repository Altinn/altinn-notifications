## This machine

- The .NET 10 SDK is the newest one when the image was built. When `global.json` asks for a newer one than
  `dotnet --list-sdks` shows, say so: the image needs rebuilding. Do not change `global.json` to fit.
- `docker` and `docker compose` run Podman. Testcontainers reaches it at `/run/docker.sock`.
- SQL Server, which the Service Bus emulator needs, has images for amd64 only. On an arm64 machine (`uname -m` prints
  `aarch64`), the `*.IntegrationTestsASB` projects, the emulator and so the Aspire stack cannot run: say so, and that
  CI runs the tests, rather than working around it.
- The Aspire CLI is the version the image was built with. When `aspire start` says the app host needs a newer one, say
  so: the image needs rebuilding. A cold start of the stack, with its first build and the emulator's containers, can
  take several minutes here, so wait with `aspire wait api --timeout 600`.
- The `*.IntegrationTestsASB` projects depend on timing: their fixture waits at most 5 minutes for SQL Server and the
  emulator to log that they are ready, and some tests assert on retry policies. On a busy or small machine, such as
  the `nested` variant, a fixture `TimeoutException` or a retry test can fail without a code change. Run one ASB
  project at a time, and rerun a project that fails that way on its own before you treat it as a regression. Report
  one that keeps failing, with its output, and run the same test on `origin/main` to compare.
- There is no browser and no desktop, so the Aspire dashboard is out of reach: use `aspire describe` and `aspire logs`
  instead. Show a running service with `curl` in a terminal recording.
