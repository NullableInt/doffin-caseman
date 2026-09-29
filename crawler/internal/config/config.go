// Package config loads and validates the crawler's runtime configuration from
// environment variables.
package config

import (
	"fmt"
	"os"
	"strconv"
	"strings"
)

type Config struct {
	DatabaseURL string

	DoffinAPIBaseURL   string
	DoffinSubscription string
	DoffinCPVCodes     []string
	DoffinRegions      []string
	DoffinPageSize     int
	DoffinFetchDetail  bool

	CrawlScheduleCron string
	LogLevel          string
}

func Load() (Config, error) {
	cfg := Config{
		DatabaseURL: os.Getenv("DATABASE_URL"),
		// api.doffin.no is the confirmed API host (verified live against a
		// real subscription key) -- NOT the *.azure-api.net docs/portal
		// hosts. See docs/doffin-api-notes.md for how this was verified.
		DoffinAPIBaseURL:   getenvDefault("DOFFIN_API_BASE_URL", "https://api.doffin.no"),
		DoffinSubscription: os.Getenv("DOFFIN_API_SUBSCRIPTION_KEY"),
		DoffinCPVCodes:     splitCSV(os.Getenv("DOFFIN_CPV_CODES")),
		DoffinRegions:      splitCSV(os.Getenv("DOFFIN_REGIONS")),
		CrawlScheduleCron:  getenvDefault("CRAWL_SCHEDULE_CRON", "0 3 * * *"),
		LogLevel:           getenvDefault("LOG_LEVEL", "info"),
	}

	pageSize, err := strconv.Atoi(getenvDefault("DOFFIN_PAGE_SIZE", "100"))
	if err != nil {
		return Config{}, fmt.Errorf("invalid DOFFIN_PAGE_SIZE: %w", err)
	}
	cfg.DoffinPageSize = pageSize

	fetchDetail, err := strconv.ParseBool(getenvDefault("DOFFIN_FETCH_DETAIL", "false"))
	if err != nil {
		return Config{}, fmt.Errorf("invalid DOFFIN_FETCH_DETAIL: %w", err)
	}
	cfg.DoffinFetchDetail = fetchDetail

	if cfg.DatabaseURL == "" {
		return Config{}, fmt.Errorf("DATABASE_URL is required")
	}
	if cfg.DoffinSubscription == "" {
		return Config{}, fmt.Errorf("DOFFIN_API_SUBSCRIPTION_KEY is required")
	}
	if len(cfg.DoffinCPVCodes) == 0 && len(cfg.DoffinRegions) == 0 {
		return Config{}, fmt.Errorf("at least one of DOFFIN_CPV_CODES or DOFFIN_REGIONS must be set")
	}

	return cfg, nil
}

func getenvDefault(key, def string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return def
}

func splitCSV(s string) []string {
	if strings.TrimSpace(s) == "" {
		return nil
	}
	parts := strings.Split(s, ",")
	out := make([]string, 0, len(parts))
	for _, p := range parts {
		p = strings.TrimSpace(p)
		if p != "" {
			out = append(out, p)
		}
	}
	return out
}
