## This machine

- The .NET 10 SDK is the newest one when the image was built. When `global.json` asks for a newer one than
  `dotnet --list-sdks` shows, say so: the image needs rebuilding. Do not change `global.json` to fit.
- `docker` and `docker compose` run Podman. Testcontainers reaches it at `/run/docker.sock`.
- SQL Server, which the Service Bus emulator needs, has images for amd64 only. On an arm64 machine (`uname -m` prints
  `aarch64`), the `*.IntegrationTestsASB` projects and the emulator cannot run: say so, and that CI runs them, rather
  than working around it.
- The `*.IntegrationTestsASB` projects depend on timing: their fixture waits at most 5 minutes for SQL Server and the
  emulator to log that they are ready, and some tests assert on retry policies. On a busy or small machine, such as
  the `nested` variant, a fixture `TimeoutException` or a retry test can fail without a code change. Run one ASB
  project at a time, and rerun a project that fails that way on its own before you treat it as a regression. Report
  one that keeps failing, with its output, and run the same test on `origin/main` to compare.
- There is no browser and no desktop. Show a running service with `curl` in a terminal recording.
