# Altinn Notifications

Altinn platform microservice for handling notifications (mail, sms, etc)
This component handles the functionality related to registering and sending notifications.

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

### Prerequisites
- [PostgreSQL](https://www.postgresql.org/download/) v15
- [pgAdmin](https://www.pgadmin.org/download/)
- [Docker](https://docs.docker.com/compose/install/)

### Setting up PostgreSQL

Ensure that both PostgreSQL and pgAdmin have been installed and start pgAdmin.

In pgAdmin
- Create database _notificationsdb_
- Create the following users with password: _Password_ (see privileges in parentheses)
  - platform_notifications_admin (superuser, canlogin)
  - platform_notifications (canlogin)
- Create schema _notifications_ in notificationsdb with owner _platform_notifications_admin_

### Setting up Kafka broker and visualization
Ensure that Dokcer has been installed and is running.

In a terminal navigate to the root of this repository
and run command `docker compose -f setup-kafka.yml up -d`

Kafdrop will be available on localhost:9000

### Running the application
The application runs on port 5090. See full details in Dockerfile.


- In a terminal navigate to/src/Altinn.Notifications
- Run `dotnet run ` or `dotnet watch`

Application is now available on localhost:5090.


