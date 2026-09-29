package store

import (
	"context"
	"fmt"
	"strings"
	"time"

	"doffin-caseman/crawler/internal/doffin"
)

// UpsertResult reports what happened to one notice during an upsert, so the
// caller can log/count inserts vs. updates vs. unchanged re-sightings.
type UpsertResult struct {
	NoticeID string
	Inserted bool
	Changed  bool
}

const upsertNoticeSQL = `
INSERT INTO notices (
    notice_id, title, buyer_name, description, notice_type, status,
    cpv_codes, region_codes, published_date, deadline, contract_value_nok,
    raw_payload, first_seen_at, last_seen_at, last_changed_at
) VALUES (
    $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, now(), now(), now()
)
ON CONFLICT (notice_id) DO UPDATE SET
    title               = EXCLUDED.title,
    buyer_name          = EXCLUDED.buyer_name,
    description         = EXCLUDED.description,
    notice_type         = EXCLUDED.notice_type,
    status              = EXCLUDED.status,
    cpv_codes           = EXCLUDED.cpv_codes,
    region_codes        = EXCLUDED.region_codes,
    published_date      = EXCLUDED.published_date,
    deadline            = EXCLUDED.deadline,
    contract_value_nok  = EXCLUDED.contract_value_nok,
    raw_payload         = EXCLUDED.raw_payload,
    last_seen_at        = now(),
    last_changed_at     = CASE
        WHEN notices.raw_payload IS DISTINCT FROM EXCLUDED.raw_payload
        THEN now()
        ELSE notices.last_changed_at
    END
RETURNING (xmax = 0) AS inserted,
          (last_changed_at = now()) AS changed_this_call;
`

const insertNoticeVersionSQL = `
INSERT INTO notice_versions (notice_id, raw_payload) VALUES ($1, $2);
`

// UpsertNotice inserts or updates a single notice, and -- only when the
// payload actually changed on an existing row -- records a snapshot in
// notice_versions so amendment history is preserved.
func (s *Store) UpsertNotice(ctx context.Context, n doffin.Notice) (UpsertResult, error) {
	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return UpsertResult{}, fmt.Errorf("beginning transaction: %w", err)
	}
	defer tx.Rollback(ctx) //nolint:errcheck

	published := parseTimeOrNil(n.PublicationDate)
	deadline := parseTimeOrNil(n.Deadline)
	buyerName := buyerNames(n.Buyer)

	var contractValue *float64
	if n.EstimatedValue != nil && n.EstimatedValue.CurrencyCode == "NOK" {
		contractValue = &n.EstimatedValue.Amount
	}

	var result UpsertResult
	result.NoticeID = n.ID

	err = tx.QueryRow(ctx, upsertNoticeSQL,
		n.ID, n.Heading, buyerName, n.Description, n.Type, n.Status,
		n.CPVCodes, n.LocationID, published, deadline, contractValue,
		n.RawPayload,
	).Scan(&result.Inserted, &result.Changed)
	if err != nil {
		return UpsertResult{}, fmt.Errorf("upserting notice %s: %w", n.ID, err)
	}

	if result.Changed && !result.Inserted {
		if _, err := tx.Exec(ctx, insertNoticeVersionSQL, n.ID, n.RawPayload); err != nil {
			return UpsertResult{}, fmt.Errorf("recording notice version for %s: %w", n.ID, err)
		}
	}

	if err := tx.Commit(ctx); err != nil {
		return UpsertResult{}, fmt.Errorf("committing upsert for %s: %w", n.ID, err)
	}

	return result, nil
}

// buyerNames joins the buyer array's distinct names -- confirmed live that a
// single notice can list the same buyer twice under different
// organizationIds (e.g. a directorate registered under two org numbers), so
// duplicates are collapsed rather than shown twice.
func buyerNames(buyers []doffin.Buyer) string {
	seen := make(map[string]bool, len(buyers))
	var names []string
	for _, b := range buyers {
		if b.Name == "" || seen[b.Name] {
			continue
		}
		seen[b.Name] = true
		names = append(names, b.Name)
	}
	return strings.Join(names, ", ")
}

// parseTimeOrNil handles both the full RFC3339 timestamps Doffin uses for
// "deadline"/"issueDate" (e.g. "2026-11-03T11:00:00Z") and the date-only
// format it uses for "publicationDate" (e.g. "2026-09-29"), confirmed live.
func parseTimeOrNil(s string) *time.Time {
	if s == "" {
		return nil
	}
	if t, err := time.Parse(time.RFC3339, s); err == nil {
		return &t
	}
	if t, err := time.Parse("2006-01-02", s); err == nil {
		return &t
	}
	return nil
}
