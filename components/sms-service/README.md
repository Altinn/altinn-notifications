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

[ Guide coming.. ]

## Setting up Kafka

To run a Kafka broker and Kafdrop (visualization and administration tool) locally you need to have Docker installed on your machine.

In a terminal navigate to the root of this repository
and run command `docker-compose -f setup-kafka.yml up -d`

Kafdrop will be available on localhost:9000
