# Altinn Notifications

Altinn platform microservice for handling notifications (mail, sms, etc)
This component handles the functionality related to registering and sending notifications.

## Architecture
Detailed architecture documentation can be found in the [docs/architecture](docs/architecture) folder.

## Project organization
This is a backend WebAPI solution written in .NET / C# following the clean architecture principles.
The solution is into four projects, each with their associated test project.

### Altinn.Notifications
The API layer that consumes services provided by _Altinn.Notifications.Core_

Relevant implementations:
- Controllers
- Program.cs


### Altinn.Notifications.Core
The domain and application layer that implements the business logic of the system.

Relevant implementations:
- Interfaces for external dependencies implemented by infrastructure and repository layer
- Domain models
- Services

### Altinn.Notifications.Integrations
The infrastructure layer that implements the interfaces defined in _Altinn.Notifications.Core_ for integrations towards 3rd-party libraries and systems.

Relevant implementations:
- Kafka producer and consumer implementation
- Clients for communicating with Altinn Platform components


### Altinn.Notifications.Persistance
The persistance layer that implements repository logic.

## Getting started

1. [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
2. Newest [Git](https://git-scm.com/downloads)
3. A code editor - we like [Visual Studio Code](https://code.visualstudio.com/download)
   - Also install [recommended extensions](https://code.visualstudio.com/docs/editor/extension-marketplace#_workspace-recommended-extensions) (e.g. [C#](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp))
4. [Podman](https://podman.io/) or another container tool such as Docker Desktop
5. [PostgreSQL](https://www.postgresql.org/download/)
6. [pgAdmin](https://www.pgadmin.org/download/)

### Setting up PostgreSQL

Ensure that both PostgreSQL and pgAdmin have been installed and start pgAdmin.

In pgAdmin
- Create database _notificationsdb_
- Create the following users with password: _Password_ (see privileges in parentheses)
  - platform_notifications_admin (superuser, canlogin)
  - platform_notifications (canlogin)
- Create schema _notifications_ in notificationsdb with owner _platform_notifications_admin_

A more detailed description of the database setup is available in [our developer handbook](https://docs.altinn.studio/community/contributing/handbook/postgres/)

### Cloning the application

Clone [Altinn Notifications repo](https://github.com/Altinn/altinn-notifications) and navigate to the folder.

```bash
git clone https://github.com/Altinn/altinn-notifications
cd altinn-notifications
```

### Setting up Kafka broker and visualization
Ensure that Docker has been installed and is running.

In a terminal navigate to the root of this repository
and run command `podman compose -f setup-kafka.yml up -d`

Kafdrop is now available at http://localhost:9000.

### Running the application with .NET

The Notifications components can be run locally when developing/debugging. Follow the install steps above if this has not already been done.

- Navigate to _src/Altinn.Notifications_, and build and run the code from there, or run the solution using you selected code editor

  ```cmd
  cd src/Notifications
  dotnet run
  ```

The notifications solution is now available locally at http://localhost:5090/.
To access swagger use http://localhost:5090/swagger.

### Testing
There is a Bruno (https://www.usebruno.com/) collection in ```<project root>/test/bruno``` with examples and testcases for the API.

Before running any tests, remember to prepare an ```.env``` file. See ```<project root>/test/bruno/.env.sample``` for an example of how to set it up.

