# Cloud Alerts Subsite – Design

Date: 30-09-2026
Status: Approved in brainstorming, pending spec review

## Goal

A new subsite in the mikkelhm-uc Umbraco site that:

1. Receives Umbraco Cloud alert webhooks (feature in development; payload and invocation already exist).
2. Stores each alert as a content node in a collection container.
3. Shows the alerts on one public listing page with filtering.

## Decisions

| Topic | Decision |
|-------|----------|
| Approach | Content nodes; server-side filtering over the published cache; query-string filters; paging |
| Auth | Static secret in the `uc-webhook-auth` header, compared in constant time |
| Secret storage | Config key `CloudAlerts:WebhookSecret` (env var `CloudAlerts__WebhookSecret` on Cloud; user-secrets or `.env` locally). Never committed |
| Listing page access | Public |
| URL | `/cloud-alerts/` (from the home node name `Cloud Alerts`; the root node name is hidden from paths, as with Ellabm; no hostname binding) |
| Publishing | Alert nodes are published on creation |
| Test alerts | Stored like other alerts (`isTest = true`); shown by default, filterable |

Out of scope: retention/cleanup of old alerts, Examine-based lookup, signature (HMAC) verification, notifications.

## Payload

```json
{
  "alertId": "6b2c86d7-c408-47d0-aa37-fc77591ff329",
  "projectAlias": "mikkelhm",
  "projectUrl": "https://www.s1.umbraco.io/project/mikkelhm",
  "environmentName": "Live",
  "timeFired": "2026-09-30T19:18:41.2585377Z",
  "alertName": "Deployment",
  "details": "[TEST] Deployment completed for mikkelhm (Live)",
  "customerMetadata": { "severity": "medium" },
  "isTest": true
}
```

`customerMetadata` is free-form. `severity` is read from it when present.

## 1. Content model

```
Cloud Alerts              website (existing Generic doc type, no template)
└── Cloud Alerts          cloudAlertsHome – listing page + collection
    └── <alert nodes>     cloudAlert – no template
```

- Doc type folder: `CloudAlerts`.
- `cloudAlertsHome`: template `CloudAlertsHome`; collection view of its children; only allowed child is `cloudAlert`; composes `sEOSection`. Allowed as a child of `website` (update `website` allowed children accordingly).
- `cloudAlert` properties:

| Alias | Editor | Source |
|-------|--------|--------|
| `alertId` | Textstring | `alertId` (idempotency key) |
| `projectAlias` | Textstring | `projectAlias` |
| `projectUrl` | Textstring | `projectUrl` |
| `environmentName` | Textstring | `environmentName` |
| `alertName` | Textstring | `alertName` |
| `details` | Textarea | `details` |
| `severity` | Textstring | `customerMetadata.severity` (empty if absent) |
| `customerMetadata` | Textarea | raw JSON of `customerMetadata` |
| `timeFired` | Date/time | `timeFired` (UTC) |
| `isTest` | Toggle | `isTest` |
| `rawPayload` | Textarea | full request body |

- Node name: `{alertName} – {projectAlias} ({environmentName}) {timeFired:dd-MM-yyyy HH:mm}` (UTC).

## 2. Webhook endpoint

`POST /umbraco/api/cloud-alerts/webhook`

| Unit | Project | Responsibility |
|------|---------|----------------|
| `CloudAlertsWebhookController` | Web | Validates the header, binds JSON, calls the ingest service, maps the result to HTTP. Follows `EllabmApiController` style |
| `CloudAlertPayload` (record) | Core | Payload shape; `CustomerMetadata` as `JsonElement?` |
| `ICloudAlertIngestService` / `CloudAlertIngestService` | Core | Finds the `cloudAlertsHome` node, checks for an existing `alertId`, creates + saves + publishes a `cloudAlert` via `IContentService`. Returns `Created(key)` / `Duplicate` / `NoContainer` |
| `CloudAlertsOptions` | Core | `WebhookSecret` bound from `CloudAlerts` section |
| Header check | Core | Pure function: configured secret + header value → valid/invalid (`CryptographicOperations.FixedTimeEquals`) |

| Case | Response |
|------|----------|
| Header missing/wrong | 401 |
| Secret not configured | 401 + warning log (fail closed) |
| Invalid body, or missing `alertId` / `timeFired` | 400 |
| No `cloudAlertsHome` node | 503 + error log |
| `alertId` already stored | 200 `{ "status": "duplicate" }` |
| New alert | 201 `{ "status": "created", "key": "<guid>" }` |
| Unexpected exception | 500, no internal details |

- Request body capped at 64 KB.
- Duplicate check scans the container's children in the published cache. Acceptable up to a few thousand items; Examine is the upgrade path if needed.
- Service stays within `Umbraco.Cms.Core` APIs so it lives in Core (see package boundaries in CLAUDE.md).

## 3. Listing page

- Template/view `cloudalertshome.cshtml`, own layout, CSS/JS in `wwwroot/cloudalerts/`, no frontend framework.
- Query-string filters (shareable URLs):

| Param | Control | Values |
|-------|---------|--------|
| `project`, `env`, `alert`, `severity` | Dropdowns | Distinct values present in the data |
| `from`, `to` | Date pickers | Inclusive, displayed DD-MM-YYYY |
| `q` | Search box | Case-insensitive match in `details` |
| `tests` | show / hide / only | Default `show` |
| `page` | Pager | 50 per page |

- Sort: `timeFired` descending.
- `CloudAlertsQuery` (Core): pure function over `IEnumerable<CloudAlertView>` + criteria → filtered page, total count, per-severity counts, facet values. No Umbraco dependencies.
- The view maps published children to `CloudAlertView`, calls the query and renders.
- UI:
  - Filter bar; summary line with total and per-severity counts (clicking a count filters by it).
  - Cards: severity badge, alert name, project linked to `projectUrl`, environment pill, TEST badge, details, time (local + relative).
  - `<details>` element per card showing `customerMetadata` and raw payload.
  - Empty state; phone-width layout; light/dark via `prefers-color-scheme`.
- JS (progressive enhancement): auto-submit on filter change, convert `timeFired` to viewer-local time. Without JS: submit button, UTC times.
- Malformed items are skipped with a warning log.

## 4. Error handling, testing, rollout

Logging: every webhook request logs `alertId` and outcome; the secret is never logged.

Testing: new project `tests/Mikkelhm.Core.Tests` (xUnit + NSubstitute; versions in `Directory.Packages.props`; added to `Mikkelhm.sln`).

| Target | Cases |
|--------|-------|
| `CloudAlertsQuery` | Each filter, tests toggle, date range bounds, sort, paging, facets, severity counts |
| Payload mapping | Required fields, severity extraction, missing `customerMetadata`, node name format |
| `CloudAlertIngestService` | Happy path creates + publishes; duplicate skipped; no container |
| Header check | Valid; wrong; missing; secret not configured |

End-to-end: local `curl` with the sample payload (with/without header), verify node and page; Cloud webhook test button after deploy. Views verified with the `RazorCompileOnBuild=true` build.

Rollout:
1. Create doc types, data types and template via the Umbraco MCP; commit the `.uda` files and regenerated models.
2. Create content nodes `Cloud Alerts` (website) → `Cloud Alerts` (cloudAlertsHome) on Live (backoffice or Deploy content transfer).
3. Set `CloudAlerts__WebhookSecret` for Live in the Cloud portal.
4. Configure the webhook: `https://mikkelhm.euwest01.umbraco.io/umbraco/api/cloud-alerts/webhook` with header `uc-webhook-auth`.
5. Update the Site Structure section in CLAUDE.md.
