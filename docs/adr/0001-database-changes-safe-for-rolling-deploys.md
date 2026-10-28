# ADR-0001: Database changes that are safe for rolling deploys and rollback

- **Status:** Proposed
- **Date:** 2026-10-28
- **Deciders:** Team Core (altinn-notifications maintainers)

**TL;DR**

- Database functions, procedures and views that change in a backward-incompatible way get a new versioned object (`_v2`, `_v3`, …). The code moves to the new object, and the old one is dropped in a later release, no earlier than 30 days after, tracked by its own cleanup issue.
- Every PR that touches the database states its **rollback class**: *patch* (roll back at will), *minor* (roll forward; rollback takes manual work) or *major* (roll forward only).
- Migrations stay small and fast: one Yuniql version folder per change, numbered right before merge, no long-running work such as populating materialized views, and pg_cron jobs handled outside Yuniql.
- The reason: during a rolling deploy, old pods run against the new schema, and Yuniql only rolls forward.

## Context

The API runs Yuniql at startup ([`WebApplicationExtensions.cs`](../../components/api/src/Altinn.Notifications.Persistence/Extensions/WebApplicationExtensions.cs)). In a Kubernetes rolling deploy, the first new pod migrates the database while the old pods keep serving traffic. For the length of the rollout, and for as long as a rollback stays on the table, **the previous version of the code must work against the new schema**.

Yuniql only rolls forward. It has no down migrations, so redeploying the previous image does not restore a function, procedure or view that a migration dropped or changed. Someone has to do that by hand.

Functions and procedures are generated into `vX.XX/01-functions-and-procedures.sql` by DbTools from one file per object in `Migration/FunctionsAndProcedures` (see [`ReadMe.txt`](../../components/api/src/Altinn.Notifications.Persistence/Migration/ReadMe.txt)). Tables, indexes and views are written by hand in the version folder. pg_cron jobs are not managed by Yuniql at all: the refresh jobs in `v0.88/01-setup-views.sql` are commented out and were set up manually.

Some Postgres behaviour narrows the options:

- Changing a function's return type requires `DROP` + `CREATE`. `CREATE OR REPLACE` cannot do it.
- `ALTER MATERIALIZED VIEW` cannot add columns, so changing a materialized view's shape means dropping and recreating it.
- `DROP` + `CREATE` of a materialized view in one transaction gives readers no error. They wait on the lock until commit and then read the new view, but they are blocked for as long as the populate takes. An old pod that then reads the new view can still fail if it expects the old columns or types.
- A slow migration runs inside pod startup and can be killed by the startup probe, which leaves the rollout stuck.

The repository already uses versioned objects in places (`cancelorder_v2`, `claim_anytime_sms_v2`). This ADR makes that the rule and adds what is needed around it.

The trigger was [#1944](https://github.com/Altinn/altinn-notifications/pull/1944) (issue [#1880](https://github.com/Altinn/altinn-notifications/issues/1880)), which first changed the DWH metrics functions and views in place. A reviewer pointed out that old pods would break while the rollout ran.

## Decision

### 1. Expand/contract for database objects

We will make backward-incompatible changes to functions, procedures and views as **new versioned objects** instead of changing them in place.

1. **Expand:** add `<name>_v2` (and `<view>_v2`, with its own pg_cron job if it is refreshed) next to the old object. Keep the DbTools file name without the version suffix, as `ReadMe.txt` says.
2. **Switch:** in the same release, the code calls the new object. Old pods keep using the old one.
3. **Contract:** drop the old object in a **separate release no earlier than 30 days later**. Create a cleanup issue when the expand PR is opened and link it from the PR. Removing an old pg_cron job is part of the cleanup.

A change counts as backward incompatible when the previous release of the code would fail or get different results against it: a changed signature or return type, removed or renamed columns, changed column types or a changed meaning of a value. Additive changes that old code ignores (a new nullable column, a new function, a new index) may be made in place.

### 2. Rollback class on every PR that touches the database

Every PR that changes the database, and the release that ships it, states one rollback class:

| Class | Meaning | Plan A | Plan B |
| --- | --- | --- | --- |
| **Patch** | Backward compatible. The previous version can be redeployed without anything else. | Roll back at will | – |
| **Minor** | Think it through first. Rolling back needs manual work, such as restoring a dropped or changed procedure, because Yuniql does not do it. | Roll forward | Roll back with the documented manual steps |
| **Major** | Breaking type changes and/or data manipulation that make rollback unrealistic without a lot of extra work. | Roll forward | Roll forward |

In practice:

- A **"Rollback class"** section in the PR description (to be added to a new `.github/pull_request_template.md`) names the class. For *minor*, it lists the manual steps for plan B. For *major*, it explains why rolling back is not possible.
- A label (`rollback/patch`, `rollback/minor`, `rollback/major`) makes the class searchable and visible in the PR list.
- Expand PRs that follow section 1 are normally *patch*. The contract PR that drops the old objects is *minor*, but it is safe because no running version has used those objects for 30 days.

The repository does not publish GitHub releases or version tags today (the `vX.XX` names are Yuniql migration folders only), so we do not tie the class to a semantic version of the service. If the team starts versioning releases, the class maps directly to the semver part to bump.

### 3. Practical rules for migrations

- **One new Yuniql version folder per change.** Yuniql runs each folder once, so changes to an already deployed folder are never executed.
- **Number the folder right before merge.** The next free number moves while a PR is in review. Renaming the folder (and regenerating `*-functions-and-procedures.sql` with DbTools) is the last step before merge. Additive migrations only need the rename. A migration that drops something must wait until its 30 days are up, whatever its number.
- **No long-running work inside a migration.** Create materialized views `WITH NO DATA` and populate them outside the migration, through the first pg_cron run or a manual `REFRESH`. The same goes for large backfills: run them as a separate job, not at pod startup.
- **pg_cron jobs are outside Yuniql.** Someone with the rights creates them and removes them by hand in each environment. The PR describes the exact `cron.schedule` / `cron.unschedule` calls, and the cleanup issue lists the jobs to remove.

### Alternatives considered

- **Change objects in place, with old and new columns side by side.** It avoids a second object, but it does not work for return-type changes (they need `DROP` + `CREATE`) or for materialized views (no `ADD COLUMN`). It also leaves compatibility columns that are hard to remove later, because nothing shows who still reads them.
- **Accept a short error window for rarely used endpoints.** The DWH export runs rarely, so a few failed calls during a rollout might be tolerable. We rejected this as a general rule: the window is not only the rollout but every rollback, the old version breaks completely when it is redeployed, and deciding per endpoint whether errors are "acceptable" is a judgement call that is easy to get wrong.

## Consequences

- Rolling deploys and rollbacks of expand releases are safe without coordination: old and new pods each use their own objects.
- Old objects live for at least 30 days, so there is some duplication in `FunctionsAndProcedures` and in the database, and every incompatible change needs a follow-up cleanup issue and PR.
- A versioned materialized view and its pg_cron job double the refresh cost until the old one is removed.
- pg_cron changes still depend on a person with database rights in every environment. Forgetting to remove a job leaves it refreshing a view nobody reads, and forgetting to create one leaves a `WITH NO DATA` view empty.
- Reviewers get a clear question to ask on every database PR: which rollback class is this, and is plan B written down?
- Follow-ups: add `.github/pull_request_template.md` with the "Rollback class" section, and create the three `rollback/*` labels.

## Example

[#1944](https://github.com/Altinn/altinn-notifications/pull/1944) (issue [#1880](https://github.com/Altinn/altinn-notifications/issues/1880)) fixes the DWH metrics export (string UUIDs, millisecond timestamps, order shipment id). Following this ADR, it adds `get_*_metrics_v2` and `*_metrics_recent_v2` with new pg_cron refresh jobs, switches the code to them and leaves the v1 functions and views in place. Rollback class: *patch*. The v1 objects and their pg_cron jobs are removed no earlier than 30 days after release, in cleanup issue #TBD.

## References

- [`Migration/ReadMe.txt`](../../components/api/src/Altinn.Notifications.Persistence/Migration/ReadMe.txt): DbTools and version folders
- [`v0.88/01-setup-views.sql`](../../components/api/src/Altinn.Notifications.Persistence/Migration/v0.88/01-setup-views.sql): the metrics views and their manually created pg_cron jobs
- Existing versioned functions: [`cancelorder.sql`](../../components/api/src/Altinn.Notifications.Persistence/Migration/FunctionsAndProcedures/cancelorder.sql) (`cancelorder_v2`), [`claimanytimesms.sql`](../../components/api/src/Altinn.Notifications.Persistence/Migration/FunctionsAndProcedures/claimanytimesms.sql)
- [Parallel Change (expand/contract)](https://martinfowler.com/bliki/ParallelChange.html)
- ADR format: [altinn-auth `docs/adr/`](https://github.com/Altinn/altinn-auth/tree/main/docs/adr)
