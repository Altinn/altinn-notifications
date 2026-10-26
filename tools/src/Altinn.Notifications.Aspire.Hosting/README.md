# Altinn.Notifications.Aspire.Hosting

Aspire hosting extensions for the local mocks under `tools/src`. The [app host](../../aspire/README.md) uses them to add the mocks and point the Notifications services to them.

It is a library, not a tool, so it has no menu, settings or secrets of its own.

## Use

Reference it from a file-based app host. `TreatProjectReferencesAsResources=false` makes Aspire compile against the library instead of treating the reference as a project to start:

```csharp
#:sdk Aspire.AppHost.Sdk@13.6.4
#:property TreatProjectReferencesAsResources=false
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

`Aspire.Hosting` in `Directory.Packages.props` has to be the version of `Aspire.AppHost.Sdk` in the app host.

## What it adds

| Method | Adds or changes |
| :--- | :--- |
| `AddTestHarness` | The control panel, with links to its tabs and the **Clear Phone** and **Clear Inbox** commands. With intercept on, a dashboard notification for each request it holds. |
| `WithTriggers` | Lets the control panel call the API's trigger endpoints, and adds **Run trigger**, **Pause triggers** and **Resume triggers**. |
| `WithTestMessageCommands` | **Send test SMS** and **Send test email**, which order an instant message from the API with a token from the mock token issuer. |
| `WithActiveSettings` | Shows settings and their values as properties of a resource in the dashboard. |
| `AddMockAuth` / `WithMockAuth` | The mock token issuer, and makes a service trust its tokens. |
| `AddAcsMock` / `WithAcsMock` | The ACS mock, and makes the email service send to it with the mock's test key. |
| `AddLinkMobilityMock` / `WithLinkMobilityMock` | The LinkMobility proxy, and connects it and the SMS service both ways: sends one way, delivery reports the other. |
| `AddPlatformMocks` / `WithPlatformMocks` | The Profile, Register, Authorization, Dialogporten, Maskinporten and condition mocks, and points the API's calls to the enabled ones. |

Every mock is a child of the control panel in the dashboard. With intercept on, the mocks hand requests they do not recognise to the control panel and wait for it to start.

## Later

`AddMockAuth` and `AddPlatformMocks` are not specific to Notifications: other Altinn services that validate Altinn tokens or call Profile, Register and Authorization could use them. If that happens, they and their mocks could move to a shared Altinn package, with the mocks shipped as packages or containers instead of being started from source.

## Tests

`tools/test/Altinn.Notifications.Tools.Tests/AspireHosting` checks the app model the extensions build, without starting anything, and the held-request notifications against a fake control panel.
