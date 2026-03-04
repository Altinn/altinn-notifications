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
*   **[PostgreSQL](https://www.postgresql.org/download/) & [pgAdmin](https://www.pgadmin.org/download/)** – Database and management tool.
*   **IDE:** [Visual Studio](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/).
*   **[Git](https://git-scm.com/)** – Version control.

---

## 🏗 Setup & Installation

### 1. Clone the Repository

```bash
git clone https://github.com/Altinn/altinn-notifications.git
cd altinn-notifications
```

### 2. Infrastructure Setup

#### Kafka

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

#### Azure Service Bus Emulator

Altinn Notifications is being migrated from Kafka to Azure Service Bus. A local ASB emulator is available for development. The `tools/asb-emulator/.env` file is pre-configured with local dev defaults. Note that the emulator exposes AMQP on port `5672` and an HTTP management endpoint on port `5300`. The connection string for local development is pre-configured in `appsettings.Development.json` of each service.

**Podman (Preferred):**
```bash
podman compose -f tools/asb-emulator/docker-compose.yaml up -d
```

**Docker:**
```bash
docker compose -f tools/asb-emulator/docker-compose.yaml up -d
```

> 🎯 **Tip:** Use [PurpleExplorer](https://github.com/philipmat/PurpleExplorer) to browse queues and messages. It supports the `UseDevelopmentEmulator=true` flag for the local emulator connection string.

### 3. Database Setup (PostgreSQL)

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
1.  Navigate to `components/api/test/bruno`.
2.  Copy `.env.sample` to `.env` and configure your local environment variables.
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
