---
name: pr-evidence
description: What an altinn-notifications pull request shows reviewers - the tests that ran and their results, and a terminal recording of the changed service handling requests. Use before opening or updating a pull request.
---

# Show the change to reviewers

A reviewer knows only the pull request and its issue, and should see that the change works without running it.

## Test results

In the body, a table with each test project you ran, such as `Altinn.Notifications.IntegrationTestsASB`, and its
passed, failed and skipped counts from `dotnet test`. Name the revision it ran on. Name each test project CI runs for
the change that you could not run here, and why, such as the ASB tests on an arm64 machine.

## The running service

Record a terminal, as the `recording` skill describes, in which the changed service runs in the Aspire stack, and
requests show the change: such as a `curl` to the API's endpoint with the response, then the order's status from
`GET /notifications/api/v1/future/shipment/{shipmentId}`, or `aspire logs` of the email or SMS service that handled
the message. Email and SMS go through to the stack's mocks, so an order can be followed to `Email_Delivered` or
`SMS_Delivered`, or to the failure its recipient picks. Start from a stack that is already up, so the recording stays
short. The API's Swagger document is at `http://localhost:5090/swagger/v1/swagger.json`.

Get the token inside the command, as `$(aspire secret get Parameters:mock-auth-password)` in the `curl`, so the
password does not show in the recording.

When a change cannot be seen from outside, such as a refactoring, say so instead and leave the recording out.

- Keep artifacts under `/home/agent/code/.artifacts/<task>/`, outside the checkout.
- Use test data, and no tokens or connection strings with real secrets.
- Keep each file within 10 MiB, and each clip short enough to follow without seeking.
- Give each recording a caption that says what it starts from and what it shows.

## Attach

Write the body with local references to the files, and run `gh` from the artifact directory with one `--attach` per
file. It uploads them and rewrites the references to hosted URLs:

```sh
gh pr create --repo Altinn/altinn-notifications --base main --head <branch> \
  --title '...' --body-file pr-body.md --attach ./service.gif
gh pr edit <number> --attach ./service.gif
```

A failed upload can still have created or updated the pull request, so look at it before retrying. Read the body back
with `gh pr view <number> --json body -q .body` and check that every attachment has a hosted URL.
