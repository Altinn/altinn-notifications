# Architecture

The Notifications solution in Altinn is made up of multiple ASP.NET Web API applications deployed as Docker containers to a Kubernetes cluster.
The solution is supported by multiple cron jobs running in the same Kubernetes cluster, an Azure Service Bus namespace, and an instance of [Azure Communication Services](https://learn.microsoft.com/en-us/azure/communication-services/overview).

## Documentation

| Page | Description |
|------|-------------|
| [Notifications API](notifications.md) | API controllers, database schema, ASB integration, cron jobs, dependencies |
| [Email service](email.md) | ASB integration, ACS delivery report routing, dependencies |
| [SMS service](sms.md) | ASB integration, Link Mobility, dependencies |
| [Azure Service Bus](asb.md) | Queue overview, retry policy, all publishers and handlers |
| [Instant notifications](instant-notifications.md) | Synchronous HTTP dispatch flow for instant email and SMS (no queue) |

## Solution diagram

![Solution diagram](diagrams/solution.svg "Solution diagram Altinn Notifications")

## Process flow diagrams

<details>
<summary>Order processing</summary>

![Order processing flow](diagrams/flowchart-order-process.svg "Flow chart for order processing")
</details>

<details>
<summary>Email notification processing</summary>

![Email notification flow](diagrams/flowchart-email-notifications-process.svg "Flow chart for email notification processing")
</details>

<details>
<summary>SMS notification processing</summary>

![SMS notification flow](diagrams/flowchart-sms-notifications-process.svg "Flow chart for SMS notification processing")
</details>

<details>
<summary>Orders chain registration</summary>

![Orders chain registration flow](diagrams/flowchart-orders-chain-registration-process.svg "Flow chart for orders chain registration")
</details>

<details>
<summary>Instant notifications</summary>

![Instant notifications flow](diagrams/flowchart-instant-notifications-process.svg "Flow chart for instant email and SMS notifications")
</details>

## System dependencies

### Internal

- **Altinn Authorization**: used to filter recipients being sent to an organization.
- **Altinn Profile**: used to retrieve recipient information.
- **Altinn Register**: used to retrieve recipient information.

### External

- [**Azure Kubernetes Services**](https://azure.microsoft.com/en-us/products/kubernetes-service): hosts the docker containers for microservices and cron jobs in a fully managed Kubernetes cluster.
- [**Azure Service Bus**](https://azure.microsoft.com/en-us/products/service-bus): hosts the message queues the microservices consume and produce messages to.
- [**PostgreSQL**](https://www.postgresql.org/): used for storage.
- [**Azure Communication Services**](https://azure.microsoft.com/en-us/products/communication-services): used to send emails.
- [**Azure Event Grid**](https://azure.microsoft.com/en-us/products/event-grid): used to route ACS email delivery report events to Azure Service Bus.
- [**LINK Mobility**](https://www.linkmobility.com/): used to send SMS.
- [**Maskinporten**](https://www.digdir.no/felleslosninger/maskinporten/869): used to generate tokens for external REST API requests.
