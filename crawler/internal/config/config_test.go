package config

import "testing"

func TestLoad_RequiresDatabaseURL(t *testing.T) {
	t.Setenv("DATABASE_URL", "")
	t.Setenv("DOFFIN_API_SUBSCRIPTION_KEY", "key")
	t.Setenv("DOFFIN_CPV_CODES", "72000000")

	if _, err := Load(); err == nil {
		t.Fatal("expected error when DATABASE_URL is missing")
	}
}

func TestLoad_RequiresAtLeastOneFilter(t *testing.T) {
	t.Setenv("DATABASE_URL", "postgres://localhost/db")
	t.Setenv("DOFFIN_API_SUBSCRIPTION_KEY", "key")
	t.Setenv("DOFFIN_CPV_CODES", "")
	t.Setenv("DOFFIN_REGIONS", "")

	if _, err := Load(); err == nil {
		t.Fatal("expected error when no CPV codes or regions are configured")
	}
}

func TestLoad_ParsesCSVFilters(t *testing.T) {
	t.Setenv("DATABASE_URL", "postgres://localhost/db")
	t.Setenv("DOFFIN_API_SUBSCRIPTION_KEY", "key")
	t.Setenv("DOFFIN_CPV_CODES", "72000000, 45000000")
	t.Setenv("DOFFIN_REGIONS", "Oslo")

	cfg, err := Load()
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	want := []string{"72000000", "45000000"}
	if len(cfg.DoffinCPVCodes) != len(want) || cfg.DoffinCPVCodes[0] != want[0] || cfg.DoffinCPVCodes[1] != want[1] {
		t.Fatalf("got CPV codes %v, want %v", cfg.DoffinCPVCodes, want)
	}
}

func TestLoad_DefaultsPageSizeAndSchedule(t *testing.T) {
	t.Setenv("DATABASE_URL", "postgres://localhost/db")
	t.Setenv("DOFFIN_API_SUBSCRIPTION_KEY", "key")
	t.Setenv("DOFFIN_CPV_CODES", "72000000")
	t.Setenv("DOFFIN_PAGE_SIZE", "")
	t.Setenv("CRAWL_SCHEDULE_CRON", "")

	cfg, err := Load()
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if cfg.DoffinPageSize != 100 {
		t.Errorf("got page size %d, want 100", cfg.DoffinPageSize)
	}
	if cfg.CrawlScheduleCron != "0 3 * * *" {
		t.Errorf("got schedule %q, want default", cfg.CrawlScheduleCron)
	}
}
