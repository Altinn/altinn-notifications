# Retry Dead Delivery Reports Tool

## Overview

This tool is designed to retry the processing of dead delivery reports in the Altinn Notifications system. When notification delivery reports fail to process and end up in the table notifications.deaddeliveryreports, 
this utility provides a mechanism to reprocess those failed reports by recreating the delivery report event grid message and posting it to the webhook endpoint.

## Purpose

The Retry Dead Delivery Reports tool addresses scenarios where:
- Delivery reports have failed to process due to temporary issues
- Manual intervention is required to reprocess failed notification delivery confirmations

## Configuration

### User Secrets

To protect sensitive information, use .NET User Secrets to store database connection strings and API access keys. This prevents accidentally committing credentials to source control.

#### Setting up User Secrets

1. Navigate to the tool's project directory:

```bash
cd src/Tools/Altinn.Notifications.Tools.RetryDeadDeliveryReports
```

2. Initialize user secrets (if not already done):
```bash	
dotnet user-secrets init
```

3. Set the required secrets:
```bash
dotnet user-secrets set "PostgreSQLSettings:ConnectionString" "<Your_Connection_String>"
dotnet user-secrets set "EventGrid:BaseUrl" "<Your_Webhook_Endpoint>"
dotnet user-secrets set "EventGrid:AccessKey" "<Your_Access_Key>"
```

### Processing Settings

The tool processes dead delivery reports within a specified ID range. Configure these values in `appsettings.json`:

- **FromId**: The starting ID for processing dead delivery reports
- **ToId**: The ending ID for processing dead delivery reports

Adjust these values based on the range of records you need to reprocess.

## Usage

### Prerequisites

- Appropriate access credentials to the Altinn Notifications system
- Database connection configured via user secrets
- Event Grid webhook endpoint and access key configured via user secrets

### Running the Tool

1. Ensure user secrets are configured (see Configuration section above)

2. Update the processing range in `appsettings.json` if needed

3. Run the tool from the project directory:
   ```bash
   cd src/Tools/Altinn.Notifications.Tools.RetryDeadDeliveryReports
   dotnet run
   ```

Or run from the solution root:
   ```bash
   dotnet run --project src/Tools/Altinn.Notifications.Tools.RetryDeadDeliveryReports/Altinn.Notifications.Tools.RetryDeadDeliveryReports.csproj
   ```

4. The tool will:
- Retrieve dead delivery reports from the database within the specified ID range
- Transform them into event grid messages
- Post the messages to the configured webhook endpoint
  - Log the processing results to the console

### Monitoring

The tool outputs progress and error information to the console. Monitor the output to ensure:
- Records are being processed successfully
- Any errors are identified and addressed
- The expected number of records are processed
