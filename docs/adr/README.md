# Architecture Decision Records (ADRs)

An ADR records one architectural decision: its **context**, the **decision** and its **consequences**. It keeps the *why* that code and git history lose.

The format follows the ADRs in [altinn-auth](https://github.com/Altinn/altinn-auth/tree/main/docs/adr).

## Rules

- Numbered sequentially, zero-padded: `0001`, `0002`, …
- Keep them short (about one page) and link to code, PRs and issues for detail.
- Once accepted, an ADR is not edited. A changed decision gets a new ADR that supersedes the old one.
- Add the ADR to the index below in the same PR.

## Index

| ADR | Title | Status |
| --- | --- | --- |
| [0001](0001-database-changes-safe-for-rolling-deploys.md) | Database changes that are safe for rolling deploys and rollback | Proposed |
