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
- Kafka consumer implementation


### Altinn.Notifications.Core
The domain and application layer that implements the business logic of the system.

Relevant implementations:
- Interfaces for external dependencies implemented by infrastructure and repository layer
- Domain models
- Services

### Altinn.Notifications.Integrations
The infrastructure layer that implements the interfaces defined in _Altinn.Notifications.Core_ for integrations towards 3rd-party libraries and systems.

Relevant implementations:
- Kafka producer implementation
- Clients for communicating with Altinn Platform components


### Altinn.Notifications.Persistance
The persistance layer that implements repository logic.

## Getting started

The fastest way to get development going is to open the main solution Altinn.Notifications.sln and selecting 'Altinn.Notifications' as the start up project from Visual Studio. Browser should open automatically to the swagger ui for the API.

Alternatively:

Start the backend in /src/Altinn.Notifications with `dotnet run` or `dotnet watch`


## Setting up PostgreSQL

To run Notifications locally you need to have PostgreSQL database installed.

- [Download PostgreSQL](https://www.postgresql.org/download/) (Currently using 14 in Azure, but 15 works locally)
- Install database server (choose your own admin password and save it some place you can find it again)
- Start pgAdmin

In pgAdmin
- Create database _notificationsdb_
- Create the following users with password: _Password_ (see privileges in paranthesis)
  - platform_notifications_admin (superuser, canlogin)
  - platform_notifications (canlogin)
- Create schema _notifications_ in notificationsdb with owner _platform_notifications_admin_

## Setting up Kafka

To run a kafka broker locally you need to have Docker installed on your machine.

In a terminal navigate to the root of this repository
and run command `docker-compose -f setup-kafka.yml up -d`
