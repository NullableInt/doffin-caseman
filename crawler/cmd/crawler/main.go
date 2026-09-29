// Command crawler runs the daily Doffin notice ingest. It runs one crawl
// pass immediately on startup (useful for a fresh docker-compose up), then
// on the schedule configured via CRAWL_SCHEDULE_CRON, until the process is
// stopped.
package main

import (
	"context"
	"log/slog"
	"os"
	"os/signal"
	"syscall"

	"github.com/robfig/cron/v3"

	"doffin-caseman/crawler/internal/config"
	"doffin-caseman/crawler/internal/crawl"
	"doffin-caseman/crawler/internal/doffin"
	"doffin-caseman/crawler/internal/store"
)

func main() {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, nil))

	cfg, err := config.Load()
	if err != nil {
		logger.Error("invalid configuration", "error", err)
		os.Exit(1)
	}

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	st, err := store.Open(ctx, cfg.DatabaseURL)
	if err != nil {
		logger.Error("failed to connect to database", "error", err)
		os.Exit(1)
	}
	defer st.Close()

	client := doffin.NewClient(cfg.DoffinAPIBaseURL, cfg.DoffinSubscription)

	runCrawl := func() {
		crawl.Run(ctx, logger, client, st, cfg.DoffinCPVCodes, cfg.DoffinRegions, cfg.DoffinPageSize)
	}

	logger.Info("running initial crawl on startup")
	runCrawl()

	c := cron.New()
	if _, err := c.AddFunc(cfg.CrawlScheduleCron, runCrawl); err != nil {
		logger.Error("invalid CRAWL_SCHEDULE_CRON", "schedule", cfg.CrawlScheduleCron, "error", err)
		os.Exit(1)
	}
	c.Start()
	logger.Info("scheduler started", "schedule", cfg.CrawlScheduleCron)

	<-ctx.Done()
	logger.Info("shutting down")
	c.Stop()
}
