package doffin

import "encoding/json"

// Notice is the crawler's parsed view of one hit from the Doffin Public
// API's search endpoint (GET /public/v2/search), confirmed live against a
// real subscription key on 2026-09-29 -- see docs/doffin-api-notes.md.
//
// RawPayload always holds the exact wire bytes for this hit, so nothing is
// lost if a field here is missing/wrong for some notice shape we haven't
// seen yet.
type Notice struct {
	ID              string          `json:"id"`
	Heading         string          `json:"heading"`
	Description     string          `json:"description"`
	Buyer           []Buyer         `json:"buyer"`
	LocationID      []string        `json:"locationId"`
	EstimatedValue  *EstimatedValue `json:"estimatedValue"`
	Type            string          `json:"type"`
	AllTypes        []string        `json:"allTypes"`
	Status          string          `json:"status"`
	IssueDate       string          `json:"issueDate"`
	Deadline        string          `json:"deadline"`
	PublicationDate string          `json:"publicationDate"`
	CPVCodes        []string        `json:"cpvCodes"`
	RawPayload      json.RawMessage `json:"-"`
}

type Buyer struct {
	ID             string `json:"id"`
	OrganizationID string `json:"organizationId"`
	Name           string `json:"name"`
}

type EstimatedValue struct {
	CurrencyCode string  `json:"currencyCode"`
	Amount       float64 `json:"amount"`
}

// SearchResponse is the crawler's parsed view of a page of search results.
// numHitsAccessible caps at 1000 regardless of numHitsTotal (an Algolia-style
// search backend limit, confirmed live) -- fine for daily incremental crawls
// sorted by publication date descending, not suitable for a full historical
// backfill.
type SearchResponse struct {
	Hits              []Notice `json:"hits"`
	NumHitsTotal      int      `json:"numHitsTotal"`
	NumHitsAccessible int      `json:"numHitsAccessible"`
}

// SearchParams holds the filters/pagination for one search request. CPVCodes
// and Regions are each sent as repeated query params (e.g.
// cpvCode=A&cpvCode=B), confirmed live to act as an OR filter -- comma-joined
// values do NOT work (confirmed: returns zero hits).
type SearchParams struct {
	CPVCodes []string
	Regions  []string
	Page     int
	PageSize int
}
