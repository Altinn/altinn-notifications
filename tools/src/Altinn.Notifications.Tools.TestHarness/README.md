# Test harness control panel

A local server with a web GUI for the local test stack. It intercepts the requests the mocks do not recognise: instead of a 404, the mock forwards the request here, the control panel holds it open, and an operator picks or writes the answer in the browser. The answer goes back through the mock to the service that made the call.

The Aspire app host runs it as the `testharness-controlpanel` resource and groups the mocks under it in the dashboard. It used to be called the simulator, which is why answers carry an `X-Simulator` header.

Use it to try answers the mocks have no scenario for, such as a 500 from Profile, a 429 from ACS or a LinkMobility `LOGON FAIL`, without changing any mock.

It also calls the API's trigger endpoints on a schedule, the way the cron jobs do in the cluster, so a local stack sends by itself. See [Triggers](#triggers).

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

## Triggers

The **Triggers** tab (`http://localhost:5096/#triggers`) lists the API's trigger endpoints. A hosted service calls each enabled one once every interval, with an empty `POST` and `Content-Type: application/json` to `{ApiBaseUrl}/notifications/api/v1/trigger/{name}`.

| Trigger | Default | In the cluster |
| :--- | :--- | :--- |
| `sendemail` | on, every 15 s | `*/1 * * * *` |
| `sendemaildaytime` | on, every 15 s | not in the documented cron jobs |
| `sendsmsanytime` | on, every 15 s | `*/1 * * * *` |
| `sendsms` (daytime, also routed as `sendsmsdaytime`) | on, every 15 s, hours `7-16` | `* 7-16 * * *` |
| `terminateexpirednotifications` | on, every 60 s | `*/15 * * * *` |
| `deleteoldstatusfeedrecords` | off, every 300 s, hours `2-5` | `*/5 2-5 * * *` |
| `pastdueoneorder`, `retryoneorder` | off, every 10 s | Not scheduled. The API's background service already does this; they are here to run by hand. |

* **Runs on its own** pauses or resumes a job. **Pause all** and **Resume all** do it for every job.
* **Every** sets the interval, and **Hours** the UTC hours the job runs in, like the hour field of a cron expression: `7-16`, `22-2`, `3,5-6`, or empty for all day. Outside its hours a job waits, as a cron job would.
* **Run now** calls the trigger once, whether the job is paused or outside its hours.
* Each job shows its last run (time, scheduled or manual, status, duration, the start of the response) and the last `HistorySize` runs. `no response` means the API could not be reached or did not answer within `TimeoutSeconds`.
* A job never overlaps itself: a run that is due while the previous one is still going is skipped, and **Run now** answers 409.
* Changes made in the GUI live in memory until the control panel restarts. The defaults can be changed with `TriggerSettings:Jobs`.

## Phone

The **Phone** tab (`http://localhost:5096/#phone`) shows the SMS the LinkMobility mock received, as the recipient's phone would. It is read-only: nothing you do there reaches the mocks or the services.

* One conversation per recipient number. Each message shows the sender name above the bubble, the time it arrived and a status that updates live: `Sent` when the mock accepted it, then `Delivered`, `Failed` or `Expired` when the mock sends its delivery report. Hover the status for the gateway state, e.g. `UNDELIV`, and the ⓘ for the reference, scenario and TTL.
* It shows delivered messages by default. **Show all** also shows the ones still on their way, rejected at once (`Invalid RCV`) or failed.
* The mock sends a copy of each send to `POST /mirror/sms` and the delivery state to `POST /mirror/sms/{reference}/status`, when its `MirrorUrl` is set. The app host sets it. The copy is posted in the background: when the control panel is slow or stopped, the copy is dropped and the send goes on as before.
* The texts and numbers are shown **unmasked**. They are local test data, and the control panel only listens on localhost. The mocks' logs stay masked.
* The last `MirrorSize` messages are kept in memory. They are gone when the control panel restarts.

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
| `GET /api/triggers` | Every trigger with its settings, state, last run and history |
| `PUT /api/triggers/{name}` | Any of `{"enabled":false,"intervalSeconds":5,"hours":"7-16"}`. 200 with the trigger, or 400. |
| `PUT /api/triggers` | `{"enabled":false}` pauses every trigger, `true` resumes them |
| `POST /api/triggers/{name}/run` | Runs the trigger now and answers with the run, or 409 when it is already running |
| `GET /api/events` | Server-sent events, one `changed` each time something changes |
| `POST /mirror/sms` | A copy of an SMS for the Phone tab: `{"sender":"Altinn","recipient":"+4799999999","text":"Hei","ttl":2880,"reference":"<gateway ref>","scenario":"Delivered"}`. 201. `id` defaults to `reference`. |
| `POST /mirror/{channel}/{id}/status` | `{"status":"Delivered","detail":"DELIVRD"}`, with `Sent`, `Delivered`, `Failed` or `Expired`. 204, or 404 for an unknown message. |
| `GET /api/mirror/{channel}` | The kept messages of `sms`, newest first |
| `DELETE /api/mirror/{channel}` | Forgets the kept messages |

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Run with Aspire

The [app host](../../aspire/README.md#test-harness-control-panel) always starts the `testharness-controlpanel` resource with the triggers on (`Triggers:Enabled`, default `true`), and passes its `Triggers:Jobs` on as `TriggerSettings:Jobs`. Set `TestHarness:Intercept` to `true` in `tools/aspire/appsettings.Local.json` to also point the mocks' `FallbackUrl` at it and let them wait for it.

## Run on its own

```bash
dotnet run --project tools/src/Altinn.Notifications.Tools.TestHarness
```

The triggers call the API on `http://localhost:5090`. Set `TriggerSettings__Enabled=false` to start them paused.

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
| `TestHarnessSettings:MirrorSize` | `200` | How many messages the Phone tab keeps. The oldest are dropped. |
| `TestHarnessSettings:DashboardUrl` | empty | The Aspire dashboard, for trace links. The app host sets it. |
| `TriggerSettings:Enabled` | `true` | `false` starts every trigger paused |
| `TriggerSettings:ApiBaseUrl` | `http://localhost:5090` | The API the triggers are sent to |
| `TriggerSettings:TimeoutSeconds` | `100` | How long a trigger call may take |
| `TriggerSettings:HistorySize` | `20` | How many runs each trigger keeps |
| `TriggerSettings:Jobs:<name>:Enabled` | see [Triggers](#triggers) | Whether the trigger runs on its own |
| `TriggerSettings:Jobs:<name>:IntervalSeconds` | see [Triggers](#triggers) | Seconds between runs |
| `TriggerSettings:Jobs:<name>:Hours` | see [Triggers](#triggers) | UTC hours, like a cron hour field. Empty is all day. |

There are no secrets.

## Limits

* Held requests, history, auto-replies, changes to the triggers and the Phone tab's messages live in memory.
* The triggers are not cron: a job runs once every interval from when the control panel started, not on the minute.
* Bodies are shown as text. A binary body is shown as broken text, and answers are always text.
* Anyone who can reach the port can answer requests and read the Phone tab's messages unmasked. It only listens on localhost.
