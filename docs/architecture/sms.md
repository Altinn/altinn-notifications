# Notifications SMS

## API

### Public API

- [DeliveryReportController](https://github.com/Altinn/altinn-notifications/blob/main/components/sms-service/src/Altinn.Notifications.Sms/Controllers/DeliveryReportController.cs):
  Endpoint receiving delivery reports in XML-format from SMS provider.
  The controller is protected with [basic authentication](https://github.com/Altinn/altinn-notifications/blob/main/components/sms-service/src/Altinn.Notifications.Sms/Configuration/BasicAuthenticationHandler.cs).

## Integrations

### Azure Service Bus

The Notifications SMS microservice has an integration towards Azure Service Bus, managed through the [Wolverine](https://wolverine.netlify.app/) framework.

**Handlers (consumers):**

- [SendSmsCommandHandler](https://github.com/Altinn/altinn-notifications/blob/main/components/sms-service/src/Altinn.Notifications.Sms.Integrations/Wolverine/Handlers/SendSmsCommandHandler.cs):
  Consumes SMS send commands from the API and submits the SMS to Link Mobility

**Publishers:**

StatusService publishes SMS send results and delivery reports back to the API via the respective ASB queues.

[Please reference the Azure Service Bus architecture section for a closer description of the ASB queue setup.](asb.md)

### Link Mobility

Link Mobility is used as service provider for sending SMS to the end users.
A client, [SmsClient](https://github.com/Altinn/altinn-notifications/blob/main/components/sms-service/src/Altinn.Notifications.Sms.Integrations/LinkMobility/SmsClient.cs)
has been implemented based on the SDK made available by Link to interact with their API.

Delivery reports from Link Mobility are pushed to the delivery report endpoint in our public API.

Altinn SMS integrates with Link Mobility through an SMS Gateway using their XML API. AT, YT and TT environments use the publicly
available test gateway, and production uses a Digdir dedicated SMS gateway.

[API documentation for the Gateway is available on Link's website.](https://wiki.pswin.com/)

## Dependencies

The microservice takes use of a range of external and Altinn services as well as .NET libraries to support the provided functionality.

### External Services

| Service | Purpose | Resources |
|-|-|-|
| Azure Service Bus | Hosts the message queues | [Documentation](https://azure.microsoft.com/en-us/products/service-bus) |
| Link Mobility | Sends out SMS to recipients and reports back | [Documentation](https://www.linkmobility.com/no/produkter/kanaler/mobil/sms) |
| Azure Monitor | Telemetry from the application is sent to Application Insights | [Documentation](https://azure.microsoft.com/en-us/products/monitor) |
| Azure Key Vault | Safeguards secrets used by the microservice | [Documentation](https://azure.microsoft.com/en-us/products/key-vault) |
| Azure Kubernetes Services (AKS) | Hosts the microservice | [Documentation](https://azure.microsoft.com/en-us/products/kubernetes-service/) |

### Altinn Services

| Service | Purpose | Resources |
|-|-|-|
| Altinn Notifications* | Service that orchestrates SMS notifications | [Source](https://github.com/Altinn/altinn-notifications/tree/main/components/api/src) |

\*Functional dependency to enable the full functionality of Altinn Notifications. Altinn Notifications generates the
SMS messages that are to be sent through this SMS service.

### .NET Libraries

| Library   | Purpose                                     | Resources                            |
| --------  | ------------------------------------------- | ---------------------------------------- |
| Link Mobility | Interact with Link Mobility XML Gateway | [Repository](https://github.com/PSWinCom/LinkMobility.PSWin.Client), [Documentation](https://github.com/PSWinCom/LinkMobility.PSWin.Client/blob/main/README.md) |
| Wolverine | Messaging framework for ASB integration | [Repository](https://github.com/JasperFx/wolverine), [Documentation](https://wolverine.netlify.app/) |
| Wolverine.AzureServiceBus | Azure Service Bus transport for Wolverine | [Repository](https://github.com/JasperFx/wolverine), [Documentation](https://wolverine.netlify.app/guide/messaging/transports/azureservicebus/) |

## Testing

Quality gates implemented for a project require an 80% code coverage for the unit and integration tests combined.
[xUnit](https://xunit.net/) is the framework used and the [Moq library](https://github.com/moq) supports mocking parts of the solution.

### Unit tests

[The unit test project is available on GitHub](https://github.com/Altinn/altinn-notifications/tree/main/components/sms-service/test/Altinn.Notifications.Sms.Tests).

### Integration tests

[The integration test project is available on GitHub](https://github.com/Altinn/altinn-notifications/tree/main/components/sms-service/test/Altinn.Notifications.Sms.IntegrationTests).

Integration tests use [Testcontainers](https://dotnet.testcontainers.org/) to spin up dependencies automatically — no manual setup is required to run them.
Remaining dependencies such as Link Mobility have been mocked.

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

The notifications SMS application runs on port 5092.

See [DockerFile](https://github.com/Altinn/altinn-notifications/blob/main/components/sms-service/Dockerfile) for details.

## Build & deploy

### Web API

- Build and Code analysis runs in a [GitHub workflow](https://github.com/Altinn/altinn-notifications/actions)
- Build of the image is done in an [Azure DevOps Pipeline](https://dev.azure.com/brreg/altinn-studio/_build?definitionId=476)
- Deploy of the image is enabled with Helm and implemented in an [Azure DevOps Release pipeline](https://dev.azure.com/brreg/altinn-studio/_release?_a=releases&view=all&definitionId=52)

## Run on local machine

Instructions on how to set up the service on local machine for development or testing is covered by
[Getting Started](https://github.com/Altinn/altinn-notifications/blob/main/getting-started.md).
