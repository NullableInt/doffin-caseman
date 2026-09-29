// Package crawl orchestrates one end-to-end crawl pass: paginate through the
// Doffin Public API for the configured CPV/region filters and upsert every
// result into the store. A failure on one page is logged and the crawl moves
// on to the next page/notice rather than aborting the whole run, so a single
// bad record doesn't block same-day ingestion of everything else.
package crawl

import (
	"context"
	"log/slog"

	"doffin-caseman/crawler/internal/doffin"
	"doffin-caseman/crawler/internal/store"
)

type Stats struct {
	Fetched  int
	Inserted int
	Updated  int
	Errors   int
}

func Run(ctx context.Context, logger *slog.Logger, client *doffin.Client, st *store.Store, cpvCodes, regions []string, pageSize int) Stats {
	var stats Stats

	page := 1
	for {
		resp, err := client.SearchNotices(ctx, doffin.SearchParams{
			CPVCodes: cpvCodes,
			Regions:  regions,
			Page:     page,
			PageSize: pageSize,
		})
		if err != nil {
			logger.Error("search request failed, stopping crawl", "page", page, "error", err)
			stats.Errors++
			logger.Info("crawl aborted early", "fetched", stats.Fetched, "inserted", stats.Inserted, "updated", stats.Updated, "errors", stats.Errors)
			return stats
		}
		if len(resp.Hits) == 0 {
			break
		}

		for _, n := range resp.Hits {
			stats.Fetched++
			result, err := st.UpsertNotice(ctx, n)
			if err != nil {
				logger.Error("upsert failed, skipping notice", "notice_id", n.ID, "error", err)
				stats.Errors++
				continue
			}
			if result.Inserted {
				stats.Inserted++
			} else if result.Changed {
				stats.Updated++
			}
		}

		logger.Info("crawled page", "page", page, "hits", len(resp.Hits), "total_hits", resp.NumHitsTotal, "accessible_hits", resp.NumHitsAccessible)

		// numHitsAccessible caps at 1000 regardless of numHitsTotal (confirmed
		// live -- an Algolia-style search backend limit), so paginating past it
		// would just loop forever without ever reaching the reported total.
		hitCeiling := resp.NumHitsTotal
		if resp.NumHitsAccessible > 0 && resp.NumHitsAccessible < hitCeiling {
			hitCeiling = resp.NumHitsAccessible
		}
		if page*pageSize >= hitCeiling {
			break
		}
		page++
	}

	logger.Info("crawl complete", "fetched", stats.Fetched, "inserted", stats.Inserted, "updated", stats.Updated, "errors", stats.Errors)
	return stats
}
