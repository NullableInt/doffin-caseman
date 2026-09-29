// Package doffin is a client for the Doffin Public API
// (https://api.doffin.no/public/v2/...), confirmed live against a real
// subscription key on 2026-09-29 -- see docs/doffin-api-notes.md for how
// each detail below was verified.
package doffin

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strconv"
	"strings"
	"time"
)

const defaultTimeout = 30 * time.Second

type Client struct {
	baseURL    string
	subKey     string
	httpClient *http.Client
}

func NewClient(baseURL, subscriptionKey string) *Client {
	return &Client{
		baseURL: strings.TrimRight(baseURL, "/"),
		subKey:  subscriptionKey,
		httpClient: &http.Client{
			Timeout: defaultTimeout,
		},
	}
}

// SearchNotices calls the Public API's search endpoint for a single page of
// results, sorted newest-first. It retries transient (5xx/network) failures
// with exponential backoff; 4xx responses are returned as errors immediately
// since retrying won't help.
//
// CPVCodes/Regions are sent as repeated query params (cpvCode=A&cpvCode=B),
// which acts as an OR filter across all configured codes/regions in a single
// request -- confirmed live; comma-joining them does NOT work (returns zero
// hits instead of an error, so this is easy to get wrong silently).
func (c *Client) SearchNotices(ctx context.Context, params SearchParams) (*SearchResponse, error) {
	q := url.Values{}
	for _, code := range params.CPVCodes {
		q.Add("cpvCode", code)
	}
	for _, region := range params.Regions {
		q.Add("location", region)
	}
	q.Set("page", strconv.Itoa(params.Page))
	q.Set("numHitsPerPage", strconv.Itoa(params.PageSize))
	q.Set("sortBy", "PUBLICATION_DATE_DESC")

	reqURL := c.baseURL + "/public/v2/search?" + q.Encode()

	var lastErr error
	for attempt := 0; attempt < 3; attempt++ {
		if attempt > 0 {
			select {
			case <-ctx.Done():
				return nil, ctx.Err()
			case <-time.After(backoff(attempt)):
			}
		}

		resp, err := c.doGet(ctx, reqURL)
		if err != nil {
			lastErr = err
			continue
		}

		body, err := io.ReadAll(resp.Body)
		resp.Body.Close()
		if err != nil {
			lastErr = fmt.Errorf("reading response body: %w", err)
			continue
		}

		if resp.StatusCode >= 500 {
			lastErr = fmt.Errorf("doffin API returned %d: %s", resp.StatusCode, truncate(body, 500))
			continue
		}
		if resp.StatusCode >= 400 {
			return nil, fmt.Errorf("doffin API returned %d: %s", resp.StatusCode, truncate(body, 500))
		}

		var out SearchResponse
		if err := json.Unmarshal(body, &out); err != nil {
			return nil, fmt.Errorf("decoding search response: %w", err)
		}

		// Attach each hit's raw bytes by re-parsing into a generic array so
		// storage never loses fields our typed struct doesn't know about yet.
		if err := attachRawPayloads(body, &out); err != nil {
			return nil, fmt.Errorf("attaching raw payloads: %w", err)
		}

		return &out, nil
	}

	return nil, fmt.Errorf("search notices failed after retries: %w", lastErr)
}

// GetNoticeDetail fetches a single notice's full eForms document via
// GET /public/v2/download/{doffinId}. Confirmed live: this returns the raw
// UBL/eForms XML (ContractNotice, etc.), NOT JSON -- a different shape
// entirely from search hits, and heavy to parse. Not called by default (the
// search endpoint's hits already carry everything the current schema needs);
// only invoked when DOFFIN_FETCH_DETAIL=true, for a future feature that
// wants the authoritative full document.
func (c *Client) GetNoticeDetail(ctx context.Context, noticeID string) ([]byte, error) {
	reqURL := c.baseURL + "/public/v2/download/" + url.PathEscape(noticeID)

	resp, err := c.doGet(ctx, reqURL)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, fmt.Errorf("reading response body: %w", err)
	}
	if resp.StatusCode >= 400 {
		return nil, fmt.Errorf("doffin API returned %d: %s", resp.StatusCode, truncate(body, 500))
	}

	return body, nil
}

func (c *Client) doGet(ctx context.Context, reqURL string) (*http.Response, error) {
	req, err := http.NewRequestWithContext(ctx, http.MethodGet, reqURL, nil)
	if err != nil {
		return nil, fmt.Errorf("building request: %w", err)
	}
	req.Header.Set("Ocp-Apim-Subscription-Key", c.subKey)
	req.Header.Set("Accept", "application/json")

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return nil, fmt.Errorf("calling doffin API: %w", err)
	}
	return resp, nil
}

// attachRawPayloads re-decodes the top-level "hits" array as raw JSON
// messages and pairs each one with the corresponding parsed Notice by index,
// so SearchResponse.Hits[i].RawPayload always has the exact wire bytes.
func attachRawPayloads(body []byte, out *SearchResponse) error {
	var envelope struct {
		Hits []json.RawMessage `json:"hits"`
	}
	if err := json.Unmarshal(body, &envelope); err != nil {
		return err
	}
	if len(envelope.Hits) != len(out.Hits) {
		return fmt.Errorf("raw hit count (%d) does not match parsed count (%d)", len(envelope.Hits), len(out.Hits))
	}
	for i := range out.Hits {
		out.Hits[i].RawPayload = envelope.Hits[i]
	}
	return nil
}

func backoff(attempt int) time.Duration {
	return time.Duration(attempt*attempt) * 500 * time.Millisecond
}

func truncate(b []byte, n int) string {
	s := string(b)
	if len(s) > n {
		return s[:n] + "..."
	}
	return s
}
