# Doffin Public API notes

Status: **confirmed live** (2026-09-29) against a real subscription key.
Everything below was verified by actually calling the API, not inferred from
third-party docs (which turned out to be wrong on the host and endpoint
paths -- kept as a cautionary note at the bottom).

## Host

**`https://api.doffin.no`** -- a custom domain, not an `*.azure-api.net`
name. Confirmed by testing the exact request the developer portal's own
"Try it" console generates.

Two other hosts you'll see referenced and should *not* use for API calls:
- `dof-notices-prod-api.developer.azure-api.net` -- the docs/portal site.
  Hitting it with an API path returns that portal's own generic 404 HTML
  page (Azure API Management's default template), not an API response.
- `dof-notices-prod-api.azure-api.net` (no `.developer`) -- a real Azure API
  Management gateway (returns proper JSON errors), but not the one Doffin's
  Public API actually runs on; every path tried against it 404'd.

## Auth

Header `Ocp-Apim-Subscription-Key: <key>`, obtained by subscribing to the
**Public** product on the developer portal
(https://dof-notices-prod-api.developer.azure-api.net/). Confirmed working.

## Search: `GET /public/v2/search`

Confirmed live, `200 application/json`. Query params (all confirmed):

| Param            | Notes |
|------------------|-------|
| `cpvCode`        | Repeat for multiple codes: `cpvCode=A&cpvCode=B` is an **OR** filter (confirmed: 14,030 hits for two codes vs. 13,513 for one). **Comma-joining does NOT work** -- `cpvCode=A,B` silently returns zero hits instead of an error. Easy to get wrong. |
| `location`       | Same repeated-param pattern as `cpvCode`, presumed (not independently retested, but the API's OR-filter design is consistent across both). **Must be a NUTS-style code, not a display name** -- confirmed `location=Oslo` and even the portal's own pre-filled example `location=Oslo og viken` both silently return zero hits, while `location=NO081` returns real results. `NO081` is confirmed to be Oslo (every hit under it was Universitetet i Oslo / Norges Bank / Oslo kommune / national directorates registered there). No confirmed code-to-name table for other regions yet -- easiest way to find one: search without a `location` filter, inspect real hits' `locationId` values for buyers you know are in the target region. |
| `numHitsPerPage` | Not `pageSize`. |
| `page`           | 1-indexed. |
| `sortBy`         | `PUBLICATION_DATE_DESC` confirmed to work; used by the crawler so new notices always sort to the front. |
| `type`           | A notice-type filter (example value seen: `NOTICE_ON_BUYER_PROFILE`); not required -- omitting it returns all types. Full vocabulary not enumerated. |

**Pagination cap, confirmed live**: `numHitsAccessible` maxes out at **1000**
regardless of `numHitsTotal` (e.g. 159,312 total unfiltered notices, only
1000 reachable via pagination) -- an Algolia-style search backend limit.
Fine for daily incremental crawling sorted newest-first (new notices are
always within the first page or two), **not** usable for a one-time full
historical backfill of everything Doffin has ever published.

### Response shape (confirmed live)

```json
{
  "numHitsTotal": 13513,
  "numHitsAccessible": 1000,
  "hits": [
    {
      "id": "2026-115111",
      "buyer": [
        {"id": "905d60b4a5fcd50b1b7e757583fd4934", "organizationId": "972417858", "name": "Direktoratet for byggkvalitet"}
      ],
      "heading": "Prekvalifisering for konkurranse om ...",
      "description": "Anskaffelsens formål er ...",
      "locationId": ["NO081"],
      "estimatedValue": {"currencyCode": "NOK", "amount": 100000000.0},
      "type": "ANNOUNCEMENT_OF_COMPETITION",
      "allTypes": ["COMPETITION", "ANNOUNCEMENT_OF_COMPETITION"],
      "status": "ACTIVE",
      "issueDate": "2026-09-24T13:35:36Z",
      "deadline": "2026-11-03T11:00:00Z",
      "publicationDate": "2026-09-29",
      "receivedTenders": null,
      "allReceivedTenders": null,
      "cpvCodes": ["72000000", "48000000"],
      "limitedDataFlag": null,
      "doffinClassicUrl": null,
      "lots": [{"heading": "...", "description": "...", "winner": []}]
    }
  ]
}
```

Notes on the fields, all confirmed live:
- `id` is the Doffin notice ID (what the rest of this system calls
  `notice_id`), e.g. `"2026-115111"`.
- `buyer` is an **array**, not a single name -- a notice can list the same
  organization twice under different `organizationId`s (seen live). The
  crawler joins distinct names into `notices.buyer_name`.
- `locationId` is an **array** of NUTS-style codes -- `notices.region_codes`
  is `TEXT[]` to match, not a scalar.
- `estimatedValue` can be `null`; when present, has a `currencyCode` that
  isn't always `NOK`. The crawler only populates `contract_value_nok` when
  `currencyCode == "NOK"`, leaving it `null` otherwise rather than storing a
  cross-currency number under a NOK-labeled column.
- `publicationDate` is **date-only** (`"2026-09-29"`), while `issueDate` and
  `deadline` are full RFC3339 timestamps with a `Z` suffix. The crawler's
  `parseTimeOrNil` tries RFC3339 first, then falls back to `2006-01-02`.
- `lots[].winner` is presumably populated for award notices; not yet seen
  live with real data (all sampled notices were open/active).

## Download: `GET /public/v2/download/{doffinId}`

Confirmed live, `200 application/octet-stream`, **not JSON**. Returns the
raw eForms/UBL XML document (`<ContractNotice xmlns="urn:oasis:names:...">`,
~40KB for a sample notice) -- the authoritative full record, but a different
shape entirely from search hits and heavy to parse.

**Decision: not used by default.** The search endpoint's hits already carry
everything the current schema needs (title, description, buyer, CPV codes,
region, deadline, publication date, status, type, estimated value) without
requiring XML parsing. `Client.GetNoticeDetail` exists and works, gated
behind `DOFFIN_FETCH_DETAIL=true`, for a future feature that specifically
wants the authoritative eForms document (e.g. displaying full lot-level
terms) -- not needed for v1 case management.

## Still unconfirmed

- Full `type` filter vocabulary beyond the one example value seen.
- Whether `location` truly supports repeated params as an OR filter the same
  way `cpvCode` does (assumed by API design consistency, not independently
  retested).
- A code-to-region-name table beyond `NO081 = Oslo`.
- Rate limits.
- `lots[].winner` shape on an actual award notice.

## Why storing raw_payload matters here

Every notice's exact API response bytes are stored verbatim in
`notices.raw_payload` (JSONB) and, on amendment, in `notice_versions`. This
already paid off once: the initial implementation was built against
third-party community docs that got the host, path, and half the field
names wrong (see below) -- if the same happens again with an unconfirmed
detail like the `type` vocabulary or `lots[].winner` shape, fixing
`types.go`'s field mappings never requires a backfill migration, only a
one-time re-parse of already-stored `raw_payload` rows.

## Postmortem: what the third-party community docs got wrong

For reference, in case you hit similarly-wrong info elsewhere: an MCP server
README and a "technical notes" file (neither official) claimed the API was
at `dof-notices-prod-api.developer.azure-api.net/public/v1/notices/search`
with a `notices` array of `{noticeId, title, publishedDate, deadline,
buyerName, description, cpvCodes}`. Every part of that was wrong except the
general shape (a paginated JSON list of notices) and the auth header name.
Moral: for this API, trust live testing over any secondary source, including
this file if it goes stale -- re-verify against a real call if something
here stops matching reality.
