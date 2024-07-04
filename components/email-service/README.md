# altinn-notifications-email

This component handles the functionality related to sending an email through Altinn Notifications.

## Project organization
This is a backend WebAPI solution written in .NET / C# following the clean architecture principles.
The solution is into three projects, each with their associated test project.

### Altinn.Notifications.Email
The API layer that consumes services provided by _Altinn.Notifications.Email.Core_

Relevant implementations:
- Program.cs
- Kafka consumer implementation

### Altinn.Notifications.Email.Core
The domain and application layer that implements the business logic of the system.

Relevant implementations:
- Interfaces for external dependencies implemented by infrastructure layer
- Domain models
- Services for handling sending of e-mails


### Altinn.Notifications.Email.Integrations
The infrastructure layer that implements the interfaces defined in _Altinn.Notifications.Email.Core_ for integrations towards 3rd-party libraries and systems.

Relevant implementations:
- Client for integrating with e-mail service
- Kafka producer implementation

## Getting started

1. [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Newest [Git](https://git-scm.com/downloads)
3. A code editor - we like [Visual Studio Code](https://code.visualstudio.com/download)
   - Also install [recommended extensions](https://code.visualstudio.com/docs/editor/extension-marketplace#_workspace-recommended-extensions) (e.g. [C#](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp))
4. [Podman](https://podman.io/) or another container tool such as Docker Desktop

### Cloning the application

Clone [Altinn Notifications Email repo](https://github.com/Altinn/altinn-notifications-email) and navigate to the folder.

```bash
git clone https://github.com/Altinn/altinn-notifications-email
cd altinn-notifications-email
```

### Setting up Kafka broker and visualization

Ensure that Docker and Podman are installed and is running.

In a terminal navigate to the root of this repository
and run command `podman compose -f setup-kafka.yml up -d`

Kafdrop is now available at http://localhost:9000.

### Set up Azure Communication Services

If you need working end to end functionality when working on
Notifications Email Azure Communication Services (ACS) needs to be set up.

Set up a service in your personal Azure account or use an existing service in a test environment.
Find the connection string in the Azure Portal under _Settings_ -> _Keys_ and add this to the configuration values.

We recommend settings it up as a user secret with the commands below.

```
cd src Altinn.Notifications.Email
dotnet user-secrets init
dotnet user-secrets set "CommunicationServicesSettings:ConnectionString" "insert-connection-string"
```

### Running the application with .NET

The Notifications Email component can be run locally when developing/debugging. Follow the install steps above if this has not already been done.

- Navigate to _src/Altinn.Notifications.Email_, and build and run the code from there, or run the solution using you selected code editor

  ```cmd
  cd src/Altinn.Notifications.Email
  dotnet run
  ```
