# Aspire how-to

Short recipes for common tasks with the local Aspire stack. The [README](README.md) describes each part and every setting. This page says what to do, and links there for the details.

Run the `aspire` commands from `tools/aspire`. Put your own settings in a git-ignored `appsettings.Local.json` in the same folder, and restart the app host after you change it.

- [Start the stack the first time](#start-the-stack-the-first-time)
- [Run it in the background](#run-it-in-the-background)
- [Send an SMS or email and see it arrive](#send-an-sms-or-email-and-see-it-arrive)
- [See what happens when delivery fails](#see-what-happens-when-delivery-fails)
- [Call the API yourself](#call-the-api-yourself)
- [Run Bruno or k6 against the stack](#run-bruno-or-k6-against-the-stack)
- [Send an order that is not instant](#send-an-order-that-is-not-instant)
- [Answer an external call yourself](#answer-an-external-call-yourself)
- [See what happens when a queue is suspended](#see-what-happens-when-a-queue-is-suspended)
- [Pick up a code change](#pick-up-a-code-change)
- [Run a component's tests](#run-a-components-tests)
- [Compare with a service in the cluster](#compare-with-a-service-in-the-cluster)
- [Use another database, or run next to localtest](#use-another-database-or-run-next-to-localtest)
- [Run the stack from another checkout](#run-the-stack-from-another-checkout)
- [Start over](#start-over)
- [Use real ACS, LinkMobility or at23](#use-real-acs-linkmobility-or-at23)

## Start the stack the first time

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), the [Aspire CLI](https://aspire.dev/get-started/install-cli/) 17 or later, and Docker or Podman. `aspire --version` shows the CLI version, and `aspire update --self` updates it.
2. Create the `notifications-db` container as shown in [Prerequisites](README.md#prerequisites). The API creates the tables when it starts.
3. Start the app host:

   ```bash
   cd tools/aspire
   aspire run -lp http
   ```

4. Open the dashboard on http://localhost:15193 and wait until `api`, `email` and `sms` are **Running**.

The `http` profile skips the dashboard's certificate and login token. Without `-lp http` the dashboard is on https://localhost:17193 and needs a trusted development certificate (`aspire certs trust`). If something already uses port 5432 or 5300, see [Use another database, or run next to localtest](#use-another-database-or-run-next-to-localtest).

## Run it in the background

`aspire start` starts the app host without keeping the terminal, and the other commands find it from the folder:

```bash
aspire start -lp http
aspire wait api                # until api is healthy, 120 seconds at most
aspire describe                # every resource with its state and URLs
aspire logs sms -n 50          # the last 50 lines of one resource's console log
aspire stop
```

The dashboard's actions also run from the terminal, as `aspire resource <resource> <command>`. A command that asks for input takes its defaults:

```bash
aspire resource sms rebuild
aspire resource testharness-controlpanel send-test-sms
aspire resource testharness-controlpanel pause-triggers
```

`aspire describe --format json` lists each resource's commands, and `aspire describe --format json` and `aspire logs --format json` give output a script can read.

## Send an SMS or email and see it arrive

1. In the dashboard, open the menu on `testharness-controlpanel` and choose **Send test SMS** or **Send test email**.
2. Keep the defaults, or enter a recipient and text. The answer says the order and shipment it made.
3. Open the **Phone** or **Inbox** link on `testharness-controlpanel`. The message shows up there as the recipient sees it, and its status changes to delivered a few seconds later.

The commands get a token from `mock-auth` and order an instant SMS or email from `api`, so the message goes through `sms` or `email` to the mock as any other order does. They only work with the default `Auth:Mode`, `mock`.

## See what happens when delivery fails

The recipient decides what the mocks do. Use **Send test SMS** or **Send test email** as above, with one of these recipients:

| Recipient | Ends as |
| :--- | :--- |
| `+4799999901` | `SMS_Failed_Undelivered` |
| `+4799999902` | `SMS_Failed_Expired` |
| `+4799999903` | `SMS_Accepted`, since no delivery report comes |
| `+4799999904` | `SMS_Failed_InvalidRecipient` |
| `bounce@example.com` | `Email_Failed_Bounced` |
| `fail@example.com` | `Email_Failed` |
| `suppressed@example.com` | `Email_Failed_SuppressedRecipient` |
| `invalid@example.com` | `Email_Failed_InvalidFormat` |
| `throttle@example.com` | `Email_Delivered`, after the email service has paused for the throttle |

All the outcomes are in the scenarios of the [LinkMobility proxy](../src/Altinn.Notifications.Tools.LinkMobilityProxy/README.md#scenarios) and the [ACS mock](../src/Altinn.Notifications.Tools.AcsMock/README.md#scenarios). To see the status from the API, call `GET /notifications/api/v1/future/shipment/{shipmentId}` with a token, as in the next section.

## Call the API yourself

The app host generates the password for `mock-auth` the first time it runs from a checkout. Fetch a token with it, and use the token against `api` on port 5090:

```bash
TOKEN=$(curl -s -u "mock:$(aspire secret get Parameters:mock-auth-password)" "http://localhost:5094/api/GetEnterpriseToken?org=ttd&scopes=altinn:serviceowner/notifications.create")
curl -H "Authorization: Bearer $TOKEN" http://localhost:5090/notifications/api/v1/future/shipment/<shipmentId>
```

The **Swagger** link on `api` lists the endpoints. [Email through the ACS mock](README.md#email-through-the-acs-mock) and [SMS through the LinkMobility proxy](README.md#sms-through-the-linkmobility-proxy) have complete examples, and [Tokens the API accepts](../src/Altinn.Notifications.Tools.MockAuth/README.md#tokens-the-api-accepts) shows how to get the other tokens.

## Run Bruno or k6 against the stack

Start the app host first. On every start it writes the token issuer's user and password where Bruno and k6 read them, so neither needs at23 credentials.

- **Bruno**: open `components/api/test/bruno` from the checkout the app host runs from, select the `v2 aspire` environment, run `test-util/new auth token`, and then the requests or folders you want. The app host writes the credentials to `.env.v2 aspire`. A Bruno app that only reads `.env` needs that file copied to `.env` first. See [Bruno](README.md#bruno), also for the Bruno CLI.
- **k6**: from `components/api/test/k6`, run a test with the secret source the app host writes:

  ```bash
  k6 run src/tests/orders-v2.js --secret-source=file=.secrets.aspire -e altinn_env=local -e ninRecipient=12345678901 --vus=1 --iterations=1
  ```

  See [Running against the local Aspire stack](../../components/api/test/k6/ReadMe.md#running-against-the-local-aspire-stack) for Docker.

## Send an order that is not instant

Orders that are not instant wait for a trigger, as in the cluster. The `testharness-controlpanel` resource calls the triggers every 15 seconds, so an order goes out within about 15 seconds of its send time.

- **Send it now**: choose **Run trigger** on `testharness-controlpanel` and pick the trigger, e.g. `sendsmsanytime` or `sendemail`.
- **Hold everything**: choose **Pause triggers**, make the orders, and choose **Resume triggers** when you want them sent.
- **See the runs**: the **Triggers** link opens the control panel's Triggers tab, with the last runs of each trigger and their responses.

An SMS with `"sendingTimePolicy":"Daytime"` is only sent between 09 and 17 Norwegian time, also locally. Use `Anytime` to test in the evening. See [Triggers](README.md#triggers) for the schedules and how to change them.

## Answer an external call yourself

The platform mocks, the ACS mock and the LinkMobility proxy answer the calls they know. To decide the answer to the others yourself, for example to see how the API handles a 500 from Profile:

1. Set `TestHarness:Intercept` in `appsettings.Local.json` and restart the app host:

   ```json
   {
     "TestHarness": { "Intercept": true }
   }
   ```

2. Do what makes the call. Calls the mocks do not know now wait in the control panel, and the dashboard shows a notification with an **Answer it** link. To try it, run `curl -i http://localhost:5131/profile/api/v1/unknown`.
3. Answer the call in the control panel. The caller gets your answer. Without an answer it gets a 504 after `TestHarness:TimeoutSeconds`, 120 by default.

To answer every call to one system the same way, e.g. while k6 runs, turn on **Auto-reply** for that system in the control panel. To answer every SMS send yourself, not only the unknown calls, also set `"LinkMobility": { "Interactive": true }`. See [Test harness control panel](README.md#test-harness-control-panel).

## See what happens when a queue is suspended

To see how a service handles a queue that is disabled in Service Bus, for example that the SMS service cannot receive:

1. Run **Set queue status** on `servicebus` in the dashboard. Pick `altinn.notifications.sms.send` and `ReceiveDisabled`. From the terminal: `aspire resource servicebus set-queue-status --queue altinn.notifications.sms.send --status ReceiveDisabled`.
2. Send a test SMS. The SMS service's log shows `MessagingEntityDisabled`, and the SMS waits on the queue.
3. Set the queue back to `Active`. The SMS service picks up the SMS and sends it.

The emulator sets every queue back to `Active` when it restarts. See [Set queue status](README.md#set-queue-status) for what each status does.

## Pick up a code change

When you change a `.cs`, `.csproj` or `.json` file in a service's `src` folder, the dashboard shows a notification that the service has changed. Choose **Rebuild** in it, or on the service's menu. **Restart** does not pick up the change, since it starts the service from the output it already built.

A change under `components/shared/src` shows the notification for all three services. A change to the SQL migration scripts shows none: rebuild `api` yourself.

## Run a component's tests

Choose **Run tests** on `api`, `email` or `sms`. It runs `dotnet test` on the component's solution, and the result shows up as a resource under the service, with its own log. Stop `api` before you run the API's tests, since its integration tests use the same database. See [Run tests](README.md#run-tests).

## Compare with a service in the cluster

Choose **Use cluster** on `api`, `email` or `sms`. The local service stops, and `localhost` on its port reaches the service in your current `kubectl` context instead. **Use local** switches back. Only HTTP goes to the cluster: Service Bus messages still go to the local emulator. See [Use cluster / Use local](README.md#use-cluster--use-local).

## Use another database, or run next to localtest

Set the container name and ports in `appsettings.Local.json`. For example, a separate database on port 5442, and the emulator's health check on 5301 because localtest uses 5300:

```json
{
  "Postgres": {
    "ContainerName": "notifications-aspire-db",
    "Port": 5442
  },
  "ServiceBus": {
    "HealthPort": 5301
  }
}
```

Create the container as in [Prerequisites](README.md#prerequisites), with the name and host port changed. To use a PostgreSQL that is not in a container, set `Postgres:UseContainer` to `false`. All settings are in [Settings](README.md#settings).

## Run the stack from another checkout

The services use fixed ports, so only one app host can run at a time. Stop the one that runs (`aspire stop`, or Ctrl+C) before you start another.

Each checkout or worktree gets its own generated credentials, and writes its own files for Bruno and k6. Open Bruno and k6 from the checkout whose app host runs. `appsettings.Local.json` is not committed, so copy it to the new checkout.

## Start over

- **New credentials**: `aspire secret delete Parameters:mock-auth-password`, or another of the names `aspire secret list` shows, while the app host is stopped. The next start generates a new one. A Bruno app that reads `.env` needs `.env.v2 aspire` copied again.
- **An empty database**: remove the container and its volume, create them again as in [Prerequisites](README.md#prerequisites), and start the app host. The API creates the tables again.

  ```bash
  podman rm -f notifications-db
  podman volume rm notifications-db-data
  ```

- **Empty Phone and Inbox tabs**: **Clear Phone** and **Clear Inbox** on `testharness-controlpanel`.

## Use real ACS, LinkMobility or at23

Each mock can be replaced by the real service in `appsettings.Local.json`:

| To | Set | See |
| :--- | :--- | :--- |
| Send real email through Azure Communication Services | `"Acs": { "Mode": "real" }`, and the connection string in the email service's user secrets | [Email through the ACS mock](README.md#email-through-the-acs-mock) |
| Send real SMS through LinkMobility's test gateway | `"LinkMobility": { "Target": "https://xml-test.pswin.com" }`, and the gateway credentials in the SMS service's user secrets | [SMS through the LinkMobility proxy](README.md#sms-through-the-linkmobility-proxy) |
| Use tokens from the Altinn test token generator | `"Auth": { "Mode": "at23" }`, and the `v2 local` environment in Bruno | [Authentication](README.md#authentication) |
| Call a real or local Profile, Register or other platform service | `"Mocks": { "Profile": { "Enabled": false } }`, or `"Mocks": { "Enabled": false }` for all | [Platform mocks](README.md#platform-mocks) |

If something does not start, see [Troubleshooting](README.md#troubleshooting), and run `aspire doctor` to check the CLI and the container runtime.
