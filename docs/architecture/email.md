# Notifications email

## Integrations

### Azure Service Bus

The Notifications email microservice has an integration towards Azure Service Bus, managed through the [Wolverine](https://wolverine.netlify.app/) framework.

**Handlers (consumers):**

- [SendEmailCommandHandler](https://github.com/Altinn/altinn-notifications/blob/main/components/email-service/src/Altinn.Notifications.Email.Integrations/Wolverine/Handlers/SendEmailCommandHandler.cs):
  Consumes email send commands from the API, submits the email to Azure Communication Services, and enqueues a status check
- [CheckEmailSendStatusHandler](https://github.com/Altinn/altinn-notifications/blob/main/components/email-service/src/Altinn.Notifications.Email.Integrations/Wolverine/Handlers/CheckEmailSendStatusHandler.cs):
  Polls Azure Communication Services for the status of an in-flight email send operation and re-enqueues itself until a terminal status is reached

**Publishers:**

StatusService publishes email send results back to the API via the email send result queue.
SendingService publishes service rate limit events when ACS rate limits are encountered.

[Please reference the Azure Service Bus architecture section for a closer description of the ASB queue setup.](asb.md)

### Azure Communication Services

Azure's email service Communication Services Email is used to send the email to the end users.
A client, [EmailServiceClient](https://github.com/Altinn/altinn-notifications/blob/main/components/email-service/src/Altinn.Notifications.Email.Integrations/Clients/EmailServiceClient.cs)
has been implemented based on the SDK made available by Microsoft to interact with their API.

ACS email delivery reports are routed to the Notifications API via Azure Event Grid and Azure Service Bus (not directly to the email service).

## Dependencies

The microservice takes use of a range of external and Altinn services as well as .NET libraries to support the provided functionality.

### External Services

| Service | Purpose | Resources |
|-|-|-|
| Azure Service Bus | Hosts the message queues | [Documentation](https://azure.microsoft.com/en-us/products/service-bus) |
| Azure Communication Services | Sends out emails to recipients and reports back | [Documentation](https://azure.microsoft.com/en-us/products/communication-services) |
| Azure Monitor | Telemetry from the application is sent to Application Insights | [Documentation](https://azure.microsoft.com/en-us/products/monitor) |
| Azure Key Vault | Safeguards secrets used by the microservice | [Documentation](https://azure.microsoft.com/en-us/products/key-vault) |
| Azure Kubernetes Services (AKS) | Hosts the microservice | [Documentation](https://azure.microsoft.com/en-us/products/kubernetes-service/) |

### Altinn Services

| Service | Purpose | Resources |
|-|-|-|
| Altinn Notifications* | Service that orchestrates email notifications | [Source](https://github.com/Altinn/altinn-notifications/tree/main/components/api/src) |

\*Functional dependency to enable the full functionality of Altinn Notifications. Altinn Notifications generates the
emails that are to be sent through this email service.

### .NET Libraries

| Library   | Purpose                                     | Resources                            |
| --------  | ------------------------------------------- | ---------------------------------------- |
| Azure.Communication.Email | Interact with Communication Services API | [Repository](https://github.com/Azure/azure-sdk-for-net), [Documentation](https://github.com/Azure/azure-sdk-for-net/blob/Azure.Communication.Email_1.0.1/sdk/communication/Azure.Communication.Email/README.md) |
| Wolverine | Messaging framework for ASB integration | [Repository](https://github.com/JasperFx/wolverine), [Documentation](https://wolverine.netlify.app/) |
| Wolverine.AzureServiceBus | Azure Service Bus transport for Wolverine | [Repository](https://github.com/JasperFx/wolverine), [Documentation](https://wolverine.netlify.app/guide/messaging/transports/azureservicebus/) |

[A full list of NuGet dependencies is available on GitHub](https://github.com/Altinn/altinn-notifications/network/dependencies).

## Testing

Quality gates implemented for a project require an 80% code coverage for the unit and integration tests combined.
[xUnit](https://xunit.net/) is the framework used and the [Moq library](https://github.com/moq) supports mocking parts of the solution.

### Unit tests

[The unit test project is available on GitHub](https://github.com/Altinn/altinn-notifications/tree/main/components/email-service/test/Altinn.Notifications.Email.Tests).

### Integration tests

[The integration test project is available on GitHub](https://github.com/Altinn/altinn-notifications/tree/main/components/email-service/test/Altinn.Notifications.Email.IntegrationTests).

Integration tests use [Testcontainers](https://dotnet.testcontainers.org/) to spin up dependencies automatically — no manual setup is required to run them.
Remaining dependencies such as Azure Communication Services have been mocked.

### Automated tests

No automated tests are set up for this component as it is considered that the integrations and availability are implicitly tested
through automated tests on the orchestrating service, Altinn Notifications.

### Use case tests

No use case tests are set up for this component as it is considered that the integrations and availability are implicitly tested
through use case tests on the orchestrating service, Altinn Notifications.

## Hosting

### Web API

The microservice runs in a Docker container hosted in AKS,
and it is deployed as a Kubernetes deployment with autoscaling capabilities.

The notifications email application runs on port 5091.

See [DockerFile](https://github.com/Altinn/altinn-notifications/blob/main/components/email-service/Dockerfile) for details.

## Build & deploy

### Web API

- Build and Code analysis runs in a [GitHub workflow](https://github.com/Altinn/altinn-notifications/actions)
- Build of the image is done in an [Azure DevOps Pipeline](https://dev.azure.com/brreg/altinn-studio/_build?definitionId=423)
- Deploy of the image is enabled with Helm and implemented in an [Azure DevOps Release pipeline](https://dev.azure.com/brreg/altinn-studio/_release?_a=releases&view=all&definitionId=48)

## Run on local machine

Instructions on how to set up the service on local machine for development or testing is covered by
[Getting Started](https://github.com/Altinn/altinn-notifications/blob/main/getting-started.md).
