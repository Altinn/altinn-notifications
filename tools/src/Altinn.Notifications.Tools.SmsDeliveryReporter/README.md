# SMS Delivery Reporter Tool

## Overview

This tool posts Link Mobility (PSWin) XML delivery reports for a batch of SMS notifications.
It reads status/gateway-reference pairs from a text file and POSTs one report per line to the
configured SMS reports endpoint using Basic authentication.

## Purpose

Use this tool when SMS delivery reports need to be submitted manually. This covers two scenarios:

- **Stuck dead delivery reports:** the SMS was accepted and sent, but the delivery report ended up
  in the dead delivery reports table with a non-failing status and was never processed to completion.
- **Missing reports:** gateway references and their final statuses are known but the reports were
  never received by the service.

## Configuration

### appsettings.json

`EndpointUrl` and `InputFile` can be set here. `EndpointUrl` defaults to the local development
address. `Username` and `Password` should be left blank and set via user secrets instead.

### User Secrets

```bash
dotnet user-secrets set "ReporterSettings:Username" "<value>"
dotnet user-secrets set "ReporterSettings:Password" "<value>"

# Optional — override the endpoint:
dotnet user-secrets set "ReporterSettings:EndpointUrl" "https://<host>/notifications/sms/api/v1/reports"
```

### Input File

The tool reads `status-vs-gateway.txt` by default (configurable via `ReporterSettings:InputFile`).
Each line must be a quoted CSV pair:

```
"Delivered","<gateway-reference-uuid>"
"Failed","<gateway-reference-uuid>"
"Failed_Expired","<gateway-reference-uuid>"
```

The file is copied to the output directory on build. Replace its contents before running.

### State Mapping

The input file contains `SmsSendResult` enum values. These are mapped back to Link Mobility
`DeliveryState` XML states as follows:

| Input file status      | XML state sent |
|------------------------|----------------|
| `Delivered`            | `DELIVRD`      |
| `Failed`               | `FAILED`       |
| `Failed_Expired`       | `EXPIRED`      |
| `Failed_Deleted`       | `DELETED`      |
| `Failed_Undelivered`   | `UNDELIV`      |
| `Failed_Rejected`      | `REJECTD`      |
| `Failed_BarredReceiver`| `BARRED`       |

## Usage

1. Set user secrets (see above).
2. Replace `status-vs-gateway.txt` with the records to process, or point `InputFile` at another path.
3. Run the tool:
```bash
cd tools/src/Altinn.Notifications.Tools.SmsDeliveryReporter
dotnet run
```
4. The tool will log each line as `OK` or `FAIL` and print a final summary.
