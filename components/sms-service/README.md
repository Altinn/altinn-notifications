# altinn-notifications-sms

This component handles the functionality related to sending an sms through Altinn Notifications.

## Project organization
This is a backend WebAPI solution written in .NET / C# following the clean architecture principles.
The solution is into three projects, each with their associated test project.

### Altinn.Notifications.Sms
The API layer that consumes services provided by _Altinn.Notifications.Sms.Core_

Relevant implementations:
- Program.cs

### Altinn.Notifications.Sms.Core
The domain and application layer that implements the business logic of the system.

Relevant implementations:
- Interfaces for external dependencies implemented by infrastructure layer
- Domain models
- Services for handling sending of sms


### Altinn.Notifications.Sms.Integrations
The infrastructure layer that implements the interfaces defined in _Altinn.Notifications.Sms.Core_ for integrations towards 3rd-party libraries and systems.

Relevant implementations:
- Client for integrating with sms service

## Getting started

## Getting started

1. [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
2. Newest [Git](https://git-scm.com/downloads)
3. A code editor - we like [Visual Studio Code](https://code.visualstudio.com/download)
   - Also install [recommended extensions](https://code.visualstudio.com/docs/editor/extension-marketplace#_workspace-recommended-extensions) (e.g. [C#](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp))
4. [Podman](https://podman.io/) or another container tool such as Docker Desktop

### Cloning the application

Clone [Altinn Notifications Sms repo](https://github.com/Altinn/altinn-notifications-sms) and navigate to the folder.

```bash
git clone https://github.com/Altinn/altinn-notifications-sms
cd altinn-notifications-sms
```

### Setting up Kafka broker and visualization

Ensure that Docker and Podman are installed and is running.

In a terminal navigate to the root of the repository called altinn-notifications
and run command `podman compose -f setup-kafka.yml up -d`

Kafdrop is now available at http://localhost:9000.

### Set up credentials for the SMS Gateway

If you need working end to end functionality when working on
Notifications Sms the connection to Link Mobility's SMS gateway needs to be set up.

We recommend settings it up as a user secret with the commands below.

```
cd src Altinn.Notifications.Sms
dotnet user-secrets init
dotnet user-secrets set "SmsGatewaySettings:Username" "insert-username"
dotnet user-secrets set "SmsGatewaySettings:Password" "insert-password"
dotnet user-secrets set "SmsDeliveryReportSettings:Username" "insert-username"
dotnet user-secrets set "SmsDeliveryReportSettings:Password" "insert-password"
```


### Running the application with .NET

The Notifications SMS component can be run locally when developing/debugging. Follow the install steps above if this has not already been done.

- Navigate to _src/Altinn.Notifications.Sms_, and build and run the code from there, or run the solution using you selected code editor

  ```cmd
  cd src/Altinn.Notifications.Sms
  dotnet run
  ```
