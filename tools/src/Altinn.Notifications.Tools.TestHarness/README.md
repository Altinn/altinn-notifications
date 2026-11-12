# Test harness control panel

A local server with a web GUI for the local test stack. It intercepts the requests the mocks do not recognise: instead of a 404, the mock forwards the request here, the control panel holds it open, and an operator picks or writes the answer in the browser. The answer goes back through the mock to the service that made the call.

The Aspire app host runs it as the `testharness-controlpanel` resource and groups the mocks under it in the dashboard. It used to be called the simulator, which is why answers carry an `X-Simulator` header.

Use it to try answers the mocks have no scenario for, such as a 500 from Profile, a 429 from ACS or a LinkMobility `LOGON FAIL`, without changing any mock.

Unlike most tools this one has no menu: it is a server that runs until you stop it.

## How a request gets here

```
api / email ──► mock (no mapping matches) ──► control panel /intercept/{system}/{path} ──► GUI
         ◄────────────── answer ◄──────────────────── held until the operator answers
```

* The ACS mock and the platform mocks forward to `{FallbackUrl}{system}/{path}` when their `FallbackUrl` is set, keeping the method, query, headers and body. With Aspire, `TestHarness:Intercept` sets it to `http://localhost:5096/intercept/`.
* The system is the first segment after `/intercept/`: `acs`, `profile`, `register`, `authorization`, `conditions`, and later `linkmobility`. The rest is the path the mock got, e.g. `/intercept/profile/profile/api/v1/users/contactpoint/unknown`.
* The request stays open until the operator answers it or `TimeoutSeconds` runs out. Then it gets 504 with `X-Simulator: timeout`.
* Every answer has an `X-Simulator` header: `operator`, `auto` or `timeout`, unless the answer sets one itself.

The mocks wait as long as the control panel does, but the caller may not. The API's clients use the default `HttpClient` timeout of 100 seconds, so with the default 120 seconds an unanswered request fails in the API before the 504 arrives. Lower `TimeoutSeconds` if you want the API to see the 504.

## The GUI

Open `http://localhost:5096`. It updates live through server-sent events.

* **Waiting** lists the held requests with system, method, path, and seconds until the timeout.
* Click one to see its headers and body, pretty-printed when it is JSON or XML. The `Authorization`, `Proxy-Authorization` and `Cookie` headers show only the scheme, e.g. `Bearer ***`, and a LinkMobility `<PW>` shows as `***`.
* **Answer**: pick a template, change the status, headers (one `Name: value` per line) and body as you like, and press **Send answer**.
* **History** keeps the last `HistorySize` handled requests: what came in, what was answered, and whether it was the operator, an auto-reply, a timeout, or the caller giving up.
* **Trace**: a request with a `traceparent` shows its trace id. Under Aspire it links to the trace in the dashboard, where the control panel's span sits under the mock's.
* **Auto-reply**: each system can answer at once with a template instead of holding the request. `hold` turns it off. Auto-replies live in memory and are reset when the control panel restarts.

## Templates

Templates are JSON files in `templates/<system>/<id>.json`. The ones in `templates/generic` are offered for every system.

```json
{
  "name": "429 Throttled, Retry-After 10",
  "status": 429,
  "headers": { "Content-Type": "application/json", "Retry-After": "10" },
  "body": { "error": { "code": "TooManyRequests", "message": "Please try again after 10 seconds." } }
}
```

* `body` is written as JSON when it is an object or array, and as is when it is a string, e.g. XML or plain text.
* `{{guid}}` becomes a new id and `{{now}}` the current UTC time, in headers and body. A template gets the same id everywhere, so `operation-id` and the body's `id` match.
* The files are read on every use, so a new or changed template shows up without a restart. With `dotnet run` they are read from the output folder, so rebuild or edit them in `bin/` while it runs.

| System | Templates |
| :--- | :--- |
| `acs` | 202 accepted with `Operation-Location`, 400 invalid address, 401 denied, 429 with `Retry-After` |
| `profile` | 200 no contact points, 404, 500 |
| `register` | 200 no party names, 404, 500 |
| `authorization` | Permit, Deny. One decision without attributes, so the API cannot match it to a user in a multi-request. |
| `conditions` | `sendNotification` true, false, a body that is not JSON |
| `linkmobility` | OK with `REF`, message `FAIL` with `Invalid RCV`, `LOGON FAIL` |
| `generic` | 200 empty, 404, 500, 503 with `Retry-After` |

## API

The GUI uses these, and they work from scripts too:

| Endpoint | What it does |
| :--- | :--- |
| `GET /api/state` | Held requests, history, auto-replies and the timeout |
| `GET /api/templates?system=acs` | The templates for a system and the generic ones, with placeholders filled in. Without `system`, all of them. |
| `POST /api/requests/{id}/answer` | `{"status":200,"headers":{"Content-Type":"application/json"},"body":"{}"}`. 204, or 404 when the request is no longer held. |
| `PUT /api/auto-replies/{system}` | `{"template":"conditions/false"}`, or `{"template":null}` to hold again |
| `GET /api/events` | Server-sent events, one `changed` each time something changes |

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Run with Aspire

The [app host](../../aspire/README.md#test-harness-control-panel) always starts the `testharness-controlpanel` resource. Set `TestHarness:Intercept` to `true` in `tools/aspire/appsettings.Local.json` to also point the mocks' `FallbackUrl` at it and let them wait for it.

## Run on its own

```bash
dotnet run --project tools/src/Altinn.Notifications.Tools.TestHarness
```

Then start a mock with its fallback pointing here, e.g. `AcsMockSettings__FallbackUrl=http://localhost:5096/intercept/`, or call the control panel yourself:

```bash
curl -i http://localhost:5096/intercept/profile/profile/api/v1/anything
```

## Settings

| Setting | Default | Description |
| :--- | :--- | :--- |
| `TestHarnessSettings:Port` | `5096` | Port the control panel listens on, on localhost only |
| `TestHarnessSettings:Intercept` | `true` | Only tells the GUI whether the mocks forward here. The mocks' `FallbackUrl` decides it. The app host sets it. |
| `TestHarnessSettings:TimeoutSeconds` | `120` | How long a request is held before it gets 504 with `X-Simulator: timeout` |
| `TestHarnessSettings:TemplatesPath` | `templates` | The template folder, relative to the output folder |
| `TestHarnessSettings:HistorySize` | `200` | How many handled requests the history keeps |
| `TestHarnessSettings:DashboardUrl` | empty | The Aspire dashboard, for trace links. The app host sets it. |

There are no secrets.

## Limits

* Held requests, history and auto-replies live in memory.
* Bodies are shown as text. A binary body is shown as broken text, and answers are always text.
* Anyone who can reach the port can answer requests. It only listens on localhost.
