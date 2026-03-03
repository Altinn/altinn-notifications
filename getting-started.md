# Getting Started

This guide will help you set up your development environment and get started with **Altinn Notifications**.

## 🚀 Quick Overview

This repository uses a mono-repo structure, consolidating multiple services into one Place.
You can work on the entire system using the root solution or focus on individual components using their specific solutions.

| Solution | File Path | Purpose |
| :--- | :--- | :--- |
| **Full Stack** | `Altinn.Notifications.sln` | Work on API, Email, SMS, and Tools simultaneously. |
| **API** | `components/api/Altinn.Notifications.API.sln` | Core Notification API logic. |
| **Email Service** | `components/email-service/Altinn.Notifications.Email.sln` | Email sending and processing. |
| **SMS Service** | `components/sms-service/Altinn.Notifications.Sms.sln` | SMS sending and processing. |
| **Tools** | `tools/Altinn.Notifications.Tools.sln` | Utility and maintenance tools. |

---

## 🛠 Prerequisites

Ensure you have the following installed:

*   **[.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)** – Required to build and run the code.
*   **[Podman](https://podman.io/) (Preferred) or [Docker](https://www.docker.com/get-started)** – For running local infrastructure (Kafka, etc.).
*   **[PostgreSQL](https://www.postgresql.org/download/) & [pgAdmin](https://www.pgadmin.org/download/)** – Database and management tool (optional if using the containerized setup below).
*   **IDE:** [Visual Studio](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/).
*   **[Git](https://git-scm.com/)** – Version control.

---

## 🏗 Setup & Installation

### 1. Clone the Repository

```bash
git clone https://github.com/Altinn/altinn-notifications.git
cd altinn-notifications
```

### 2. Infrastructure Setup (Kafka)

Altinn Notifications uses Kafka for message queuing. Start the local Kafka instance using Podman or Docker.

**Podman (Preferred):**
```bash
podman compose -f tools/dev-setup/setup-kafka.yml up -d
```

**Docker:**
```bash
docker compose -f tools/dev-setup/setup-kafka.yml up -d
```

> 🎯 **Tip:** You can access the **Kafdrop** UI at `http://localhost:9000` to monitor your topics.

### 3. Database Setup (PostgreSQL)

#### Option A: Automated Setup (Recommended)

Run the setup script to start a PostgreSQL 17 container with the required database and roles:

**Podman (Preferred):**
```bash
bash tools/dev-setup/setup-db.sh
```

**Docker:** Replace `podman` with `docker` in `tools/dev-setup/setup-db.sh` before running.

This will:
*   Start a PostgreSQL 17 container on `localhost:5432`
*   Create the `notificationsdb` database
*   Create the `platform_notifications_admin` and `platform_notifications` roles
*   Configure `max_connections` for local development

Database migrations are applied automatically by the application on startup.

To stop the database:
```bash
podman compose -f tools/dev-setup/setup-db.yml down
```

#### Option B: Manual Setup

1.  Ensure PostgreSQL is running.
2.  Open **pgAdmin** and connect to your local server.
3.  Create a new database named: `notificationsdb`
4.  Create the following **Login/Group Roles** (set password to `Password` for local dev):
    *   `platform_notifications_admin` (Grant: `Superuser`, `Can login`)
    *   `platform_notifications` (Grant: `Can login`)

---

## 🏃 Running the Application

### Option A: Run Everything (Visual Studio)

1.  Open `Altinn.Notifications.sln` in Visual Studio.
2.  Set the **Startup Projects** to multiple:
    *   `Altinn.Notifications` (API)
    *   `Altinn.Notifications.Email` (Email Worker)
    *   `Altinn.Notifications.Sms` (Sms Worker)
3.  Press **F5** to debug.

### Option B: Run Specific Components (CLI)

You can run individual services from the command line.

**1.  Notifications API**
   *   URL: `http://localhost:5090/`
   *   Swagger: `http://localhost:5090/swagger`

   ```bash
   cd components/api/src/Altinn.Notifications
   dotnet run
   ```

**2. Email Service**

   ```bash
   cd components/email-service/src/Altinn.Notifications.Email
   dotnet run
   ```

**3. SMS Service**

   ```bash
   cd components/sms-service/src/Altinn.Notifications.Sms
   dotnet run
   ```

---

## ⚙️ Configuration (User Secrets)

To enable end-to-end functionality (like actually sending emails or SMS), you need to configure connection strings and credentials. We use **User Secrets** to keep these out of source control.

### Email Service (Azure Communication Services)

1.  Obtain an ACS connection string from the Azure Portal.
2.  Run:

```bash
cd components/email-service/src/Altinn.Notifications.Email
dotnet user-secrets init
dotnet user-secrets set "CommunicationServicesSettings:ConnectionString" "<your-connection-string>"
```

### SMS Service (Link Mobility)

1.  Obtain Link Mobility gateway credentials.
2.  Run:

```bash
cd components/sms-service/src/Altinn.Notifications.Sms
dotnet user-secrets init
dotnet user-secrets set "SmsGatewaySettings:Username" "<username>"
dotnet user-secrets set "SmsGatewaySettings:Password" "<password>"
dotnet user-secrets set "SmsDeliveryReportSettings:Username" "<username>"
dotnet user-secrets set "SmsDeliveryReportSettings:Password" "<password>"
```

---

## 🧪 Testing

### Running Unit & Integration Tests

You can run tests for the entire solution or per component.

**Full Suite:**
```bash
dotnet test Altinn.Notifications.sln
```

**Per Component:**
```bash
dotnet test components/api/Altinn.Notifications.API.sln
dotnet test components/email-service/Altinn.Notifications.Email.sln
dotnet test components/sms-service/Altinn.Notifications.Sms.sln
```

### API Testing with Bruno

We use [Bruno](https://www.usebruno.com/) for API testing.

#### Local Mock Services

The Notifications API depends on several external platform services. To run Bruno tests locally,
we provide a mock-services project that replaces all external dependencies with local WireMock stubs
and a JWT token generator.

**Architecture overview:**

```
Bruno tests
  │
  ▼
Notifications API (localhost:5090)
  ├── Auth/OIDC ──────────► Token Generator + JWKS   (localhost:5101, custom Kestrel)
  ├── Profile ────────────► WireMock                  (localhost:5030)
  ├── Register ───────────► WireMock                  (localhost:5020)
  ├── Authorization PDP ──► WireMock                  (localhost:5050)
  ├── Condition endpoints ► WireMock                  (localhost:5199)
  └── Kafka ──────────────► (localhost:9092, required)
        │
        ▼
  PastDueOrdersConsumer (in-process) ──► Profile mock (5030)
        │
        ▼
  [email.queue / sms.queue Kafka topics]
        │
        ▼
  Email Service (localhost:5190)  ──► MockEmailServiceClient (in-process)
  SMS Service   (localhost:5170)  ──► MockSmsClient (in-process)
        │
        ▼
  [email.status / sms.status Kafka topics] ──► StatusConsumers (in API) ──► Status feed
```

##### Quick Start

```bash
# 1. Prerequisites: Kafka and PostgreSQL must be running
podman compose -f tools/dev-setup/setup-kafka.yml up -d
bash tools/dev-setup/setup-db.sh

# 2. Start all services (mock services + email service + SMS service)
#    This starts: WireMock stubs, Token/OIDC, TriggerScheduler,
#    Email service (port 5190, mock ACS client), SMS service (port 5170, mock Link Mobility client)
bash tools/dev-setup/start-mock-services.sh

# 3. Start the Notifications API (in a separate terminal)
dotnet run --project components/api/src/Altinn.Notifications

# 4. Set up Bruno environment
cp components/api/test/bruno/.env.local.sample components/api/test/bruno/.env

# 5. Run a Bruno test (CLI)
cd components/api/test/bruno
npx @usebruno/cli run "v2 (future)/create-notifications/Fulfilling eForvaltningsforskriften §8 NIN.bru" --env "v2 local"

# 6. Wait ~10 seconds for TriggerScheduler to process orders, then check the status feed:
TOKEN=$(curl -s -u mock:mock "http://localhost:5101/api/GetEnterpriseToken?env=local&scopes=altinn:serviceowner/notifications.create&org=ttd")
curl -H "Authorization: Bearer $TOKEN" "http://localhost:5090/notifications/api/v1/future/shipment/feed?seq=0"
```

##### Verify the Environment

Check that all services are running before testing:

```bash
# Token generator + OIDC
curl http://localhost:5101/authentication/api/v1/openid/.well-known/openid-configuration

# Generate a token
curl -u mock:mock "http://localhost:5101/api/GetEnterpriseToken?env=local&scopes=altinn:serviceowner/notifications.create&org=ttd"

# Profile mock
curl -X POST http://localhost:5030/profile/api/v1/users/contactpoint/lookup \
  -H "Content-Type: application/json" -d '{"nationalIdentityNumbers":["12345678901"]}'

# Register mock
curl -X POST http://localhost:5020/register/api/v1/parties/nameslookup \
  -H "Content-Type: application/json" -d '{"parties":[{"ssn":"12345678901"}]}'

# Notifications API health
curl http://localhost:5090/health
```

##### Trigger Order Processing

The mock-services startup includes a **TriggerScheduler** that automatically calls the trigger
endpoints every 5 seconds, so orders are processed without manual intervention.

If you need to trigger manually (e.g., when running services individually), the endpoints are:

```bash
# Process past-due orders (fetches orders from DB → Kafka → creates notifications)
curl -X POST http://localhost:5090/notifications/api/v1/trigger/pastdueorders

# Dispatch email and SMS notifications to Kafka queues
curl -X POST http://localhost:5090/notifications/api/v1/trigger/sendemail
curl -X POST http://localhost:5090/notifications/api/v1/trigger/sendsmsanytime

# Other maintenance triggers:
curl -X POST http://localhost:5090/notifications/api/v1/trigger/terminateexpirednotifications
curl -X POST http://localhost:5090/notifications/api/v1/trigger/deleteoldstatusfeedrecords
```

##### What Works End-to-End

The local mock setup provides a **complete end-to-end pipeline**:

- Token generation and JWT validation (OIDC/JWKS)
- Order creation (201 Created) with full validation
- Profile contact point lookups (mock data)
- Register party name lookups (mock data)
- Authorization decisions (always Permit)
- Send condition evaluation (true/false/corrupt endpoints)
- Kafka order processing pipeline (order → notifications in DB)
- Email/SMS notification creation and publishing to Kafka queues
- **Email delivery simulation** — the real Email service runs with `MockEmailServiceClient`, which replaces Azure Communication Services and returns `Delivered` for all emails
- **SMS delivery simulation** — the real SMS service runs with `MockSmsClient`, which replaces Link Mobility and returns a successful gateway reference for all SMS messages
- **Automatic trigger scheduling** — `TriggerScheduler` calls `/trigger/pastdueorders`, `/trigger/sendemail`, and `/trigger/sendsmsanytime` every 5 seconds
- **Status feed population** — delivery status flows back through Kafka and populates the shipment status feed

##### How It Works

The `start-mock-services.sh` script launches three processes:

1. **Mock services** — WireMock stubs (Profile, Register, Authorization, SMS/Email, Conditions), Token/OIDC generator, and TriggerScheduler
2. **Email service** (port 5190) — runs in Development mode with `MockEmailServiceClient` replacing Azure Communication Services
3. **SMS service** (port 5170) — runs in Development mode with `MockSmsClient` replacing Link Mobility

The mock clients are injected via DI "last wins" semantics: `AddIntegrationServices` registers
the real client first, then the dev-mode override registers the mock client afterwards.

#### Testing Against Remote Environments

1.  Navigate to `components/api/test/bruno`.
2.  Copy `.env.sample` to `.env` and configure your environment variables for the target environment.
3.  Open the collection in Bruno to run requests.

---

## 📦 Containerization

To build container images locally:

**Podman:**
```bash
podman build -t notifications-api -f components/api/Dockerfile .
podman build -t notifications-email -f components/email-service/Dockerfile .
podman build -t notifications-sms -f components/sms-service/Dockerfile .
```

**Docker:**
```bash
docker build -t notifications-api -f components/api/Dockerfile .
docker build -t notifications-email -f components/email-service/Dockerfile .
docker build -t notifications-sms -f components/sms-service/Dockerfile .
```

---

## 📚 Additional Resources

*   [Architecture Documentation](docs/architecture/)
*   [API Readme](components/api/README.md)
*   [Email Service Readme](components/email-service/README.md)
*   [SMS Service Readme](components/sms-service/README.md)
