# Altinn.Notifications.Aspire.Hosting

Aspire hosting extensions for the local mocks under `tools/src`. The [app host](../../aspire/README.md) uses them to add the mocks and point the Notifications services to them.

It is a library, not a tool, so it has no menu, settings or secrets of its own.

## Use

Reference it from a file-based app host. Aspire compiles against the library and does not start it, since only `AddDotnetProject` makes a project a resource:

```csharp
#:package Aspire.Hosting.AppHost
#:package Aspire.Hosting.Dotnet
#:project ../src/Altinn.Notifications.Aspire.Hosting/Altinn.Notifications.Aspire.Hosting.csproj

var controlPanel = builder.AddTestHarness("testharness-controlpanel", 5096, intercept: false, timeoutSeconds: 120)
    .WithTriggers(api, enabled: true, builder.Configuration.GetSection("Triggers:Jobs"));

var auth = builder.AddMockAuth("mock-auth", 5094, controlPanel);
api.WithMockAuth(auth);
controlPanel.WithTestMessageCommands(api, auth);

email.WithAcsMock(builder.AddAcsMock("mock-acs", 5095, controlPanel));
sms.WithLinkMobilityMock(builder.AddLinkMobilityMock("wiremock-linkmobility", 5093, controlPanel));
api.WithPlatformMocks(builder.AddPlatformMocks("wiremock-platform", controlPanel, builder.Configuration.GetSection("Mocks")));
```

The extensions add the mocks with `AddDotnetProject` and return `IResourceBuilder<DotnetProjectResource>`, so the services they connect to have to be added with `AddDotnetProject` too. `Aspire.Hosting`, `Aspire.Hosting.AppHost` and `Aspire.Hosting.Dotnet` in `Directory.Packages.props` have to be at the same version.

## What it adds

| Method | Adds or changes |
| :--- | :--- |
| `AddTestHarness` | The control panel, with links to its tabs and the **Clear Phone** and **Clear Inbox** commands. With intercept on, a dashboard notification for each request it holds. |
| `WithTriggers` | Lets the control panel call the API's trigger endpoints, and adds **Run trigger**, **Pause triggers** and **Resume triggers**. |
| `WithTestMessageCommands` | **Send test SMS** and **Send test email**, which order an instant message from the API with a token from the mock token issuer. |
| `WithQueueStatusCommand` | **Set queue status** on the Service Bus emulator's resource, which suspends a queue. It is enabled while the emulator is healthy. |
| `WithActiveSettings` | Shows settings and their values as properties of a resource in the dashboard. |
| `AddMockAuth` / `WithMockAuth` | The mock token issuer, and makes a service trust its tokens. |
| `AddAcsMock` / `WithAcsMock` | The ACS mock, and makes the email service send to it with the mock's access key. |
| `AddLinkMobilityMock` / `WithLinkMobilityMock` | The LinkMobility proxy, and connects it and the SMS service both ways: sends one way, delivery reports the other. |
| `AddPlatformMocks` / `WithPlatformMocks` | The Profile, Register, Authorization, Dialogporten, Maskinporten and condition mocks, and points the API's calls to the enabled ones. |

The shared secrets (`mock-auth`'s password, the ACS mock's access key and the LinkMobility delivery report password) are secret parameters, generated on the app host's first run and kept in its Aspire secrets, so they stay the same between runs. `GetMockAuthCredentials` gives the token issuer's, for clients outside Aspire. See [Generated credentials](../../aspire/README.md#generated-credentials).

Every mock is a child of the control panel in the dashboard. With intercept on, the mocks hand requests they do not recognise to the control panel and wait for it to start.

## Later

`AddMockAuth` and `AddPlatformMocks` are not specific to Notifications: other Altinn services that validate Altinn tokens or call Profile, Register and Authorization could use them. If that happens, they and their mocks could move to a shared Altinn package, with the mocks shipped as packages or containers instead of being started from source.

## Tests

`tools/test/Altinn.Notifications.Tools.Tests/AspireHosting` checks the app model the extensions build, without starting anything, and the held-request notifications against a fake control panel.
