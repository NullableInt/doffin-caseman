package doffin

import (
	"context"
	"net/http"
	"net/http/httptest"
	"net/url"
	"testing"
)

func TestSearchNotices_SetsAuthHeaderAndParsesResponse(t *testing.T) {
	var gotKey string
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		gotKey = r.Header.Get("Ocp-Apim-Subscription-Key")
		w.Header().Set("Content-Type", "application/json")
		w.Write([]byte(`{
			"hits": [
				{"id": "2024-1", "heading": "Renhold", "cpvCodes": ["90910000"], "buyer": [{"name": "Oslo kommune"}]}
			],
			"numHitsTotal": 1, "numHitsAccessible": 1
		}`))
	}))
	defer server.Close()

	client := NewClient(server.URL, "test-key")
	resp, err := client.SearchNotices(context.Background(), SearchParams{PageSize: 100, Page: 1})
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	if gotKey != "test-key" {
		t.Errorf("subscription key header = %q, want %q", gotKey, "test-key")
	}
	if len(resp.Hits) != 1 || resp.Hits[0].ID != "2024-1" {
		t.Fatalf("unexpected hits: %+v", resp.Hits)
	}
	if len(resp.Hits[0].RawPayload) == 0 {
		t.Error("expected RawPayload to be populated")
	}
}

func TestSearchNotices_SendsRepeatedCPVAndRegionParams(t *testing.T) {
	var gotQuery string
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		gotQuery = r.URL.RawQuery
		w.Header().Set("Content-Type", "application/json")
		w.Write([]byte(`{"hits": [], "numHitsTotal": 0, "numHitsAccessible": 0}`))
	}))
	defer server.Close()

	client := NewClient(server.URL, "test-key")
	_, err := client.SearchNotices(context.Background(), SearchParams{
		CPVCodes: []string{"72000000", "79420000"},
		Regions:  []string{"Oslo"},
		Page:     1,
		PageSize: 50,
	})
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	parsed, err := url.ParseQuery(gotQuery)
	if err != nil {
		t.Fatalf("parsing recorded query: %v", err)
	}
	// Repeated params, not comma-joined -- comma-joining is confirmed live to
	// silently return zero hits instead of erroring, so this must stay as
	// separate values.
	if got := parsed["cpvCode"]; len(got) != 2 || got[0] != "72000000" || got[1] != "79420000" {
		t.Errorf("cpvCode params = %v, want [72000000 79420000]", got)
	}
	if got := parsed["location"]; len(got) != 1 || got[0] != "Oslo" {
		t.Errorf("location params = %v, want [Oslo]", got)
	}
	if parsed.Get("numHitsPerPage") != "50" {
		t.Errorf("numHitsPerPage = %q, want 50", parsed.Get("numHitsPerPage"))
	}
}

func TestSearchNotices_RetriesOn5xxThenFails(t *testing.T) {
	calls := 0
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		calls++
		w.WriteHeader(http.StatusServiceUnavailable)
	}))
	defer server.Close()

	client := NewClient(server.URL, "test-key")
	_, err := client.SearchNotices(context.Background(), SearchParams{PageSize: 10, Page: 1})
	if err == nil {
		t.Fatal("expected error after repeated 5xx responses")
	}
	if calls != 3 {
		t.Errorf("got %d attempts, want 3", calls)
	}
}

func TestSearchNotices_DoesNotRetryOn4xx(t *testing.T) {
	calls := 0
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		calls++
		w.WriteHeader(http.StatusUnauthorized)
	}))
	defer server.Close()

	client := NewClient(server.URL, "bad-key")
	_, err := client.SearchNotices(context.Background(), SearchParams{PageSize: 10, Page: 1})
	if err == nil {
		t.Fatal("expected error on 401")
	}
	if calls != 1 {
		t.Errorf("got %d attempts, want 1 (no retry on 4xx)", calls)
	}
}
