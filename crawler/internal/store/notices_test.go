// These tests exercise the real upsert SQL against a real PostgreSQL
// instance spun up via testcontainers-go, since the upsert/version logic in
// notices.go depends on Postgres-specific behavior (xmax, JSONB equality,
// ON CONFLICT) that a mock can't meaningfully verify. Requires a working
// Docker/Podman daemon; skipped automatically if none is reachable.
package store

import (
	"context"
	"encoding/json"
	"os"
	"path/filepath"
	"runtime"
	"testing"
	"time"

	"github.com/golang-migrate/migrate/v4"
	_ "github.com/golang-migrate/migrate/v4/database/postgres"
	_ "github.com/golang-migrate/migrate/v4/source/file"
	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/testcontainers/testcontainers-go"
	"github.com/testcontainers/testcontainers-go/wait"

	"doffin-caseman/crawler/internal/doffin"
)

func newTestStore(t *testing.T) *Store {
	t.Helper()
	ctx := context.Background()

	req := testcontainers.ContainerRequest{
		Image:        "postgres:16-alpine",
		ExposedPorts: []string{"5432/tcp"},
		Env: map[string]string{
			"POSTGRES_DB":       "doffin_test",
			"POSTGRES_USER":     "doffin",
			"POSTGRES_PASSWORD": "doffin",
		},
		WaitingFor: wait.ForListeningPort("5432/tcp").WithStartupTimeout(60 * time.Second),
	}
	container, err := testcontainers.GenericContainer(ctx, testcontainers.GenericContainerRequest{
		ContainerRequest: req,
		Started:          true,
	})
	if err != nil {
		t.Skipf("skipping: could not start postgres testcontainer (no docker daemon?): %v", err)
	}
	t.Cleanup(func() { _ = container.Terminate(ctx) })

	host, err := container.Host(ctx)
	if err != nil {
		t.Fatalf("getting container host: %v", err)
	}
	port, err := container.MappedPort(ctx, "5432")
	if err != nil {
		t.Fatalf("getting mapped port: %v", err)
	}

	dsn := "postgres://doffin:doffin@" + host + ":" + port.Port() + "/doffin_test?sslmode=disable"

	applyMigrations(t, dsn)

	pool, err := pgxpool.New(ctx, dsn)
	if err != nil {
		t.Fatalf("connecting to test database: %v", err)
	}
	t.Cleanup(pool.Close)

	return &Store{pool: pool}
}

func applyMigrations(t *testing.T, dsn string) {
	t.Helper()

	migrationsDir := migrationsPath(t)
	m, err := migrate.New("file://"+migrationsDir, dsn)
	if err != nil {
		t.Fatalf("creating migrator: %v", err)
	}
	if err := m.Up(); err != nil && err != migrate.ErrNoChange {
		t.Fatalf("applying migrations: %v", err)
	}
}

// migrationsPath finds db/migrations relative to this test file, so tests
// pass regardless of the working directory `go test` is invoked from.
func migrationsPath(t *testing.T) string {
	t.Helper()
	_, thisFile, _, ok := runtime.Caller(0)
	if !ok {
		t.Fatal("could not determine test file path")
	}
	// this file: crawler/internal/store/notices_test.go -> repo root is four dirs up
	dir := filepath.Dir(thisFile)
	root := filepath.Join(dir, "..", "..", "..")
	migrations := filepath.Join(root, "db", "migrations")
	if _, err := os.Stat(migrations); err != nil {
		t.Fatalf("db/migrations not found at %s: %v", migrations, err)
	}
	return migrations
}

func rawNotice(t *testing.T, n doffin.Notice) doffin.Notice {
	t.Helper()
	b, err := json.Marshal(n)
	if err != nil {
		t.Fatalf("marshaling notice: %v", err)
	}
	n.RawPayload = b
	return n
}

func TestUpsertNotice_InsertsNewNotice(t *testing.T) {
	s := newTestStore(t)
	ctx := context.Background()

	n := rawNotice(t, doffin.Notice{ID: "2024-1", Heading: "Renhold", CPVCodes: []string{"90910000"}})
	result, err := s.UpsertNotice(ctx, n)
	if err != nil {
		t.Fatalf("upsert failed: %v", err)
	}
	if !result.Inserted {
		t.Error("expected Inserted=true for a new notice")
	}
}

func TestUpsertNotice_UnchangedPayloadDoesNotBumpLastChanged(t *testing.T) {
	s := newTestStore(t)
	ctx := context.Background()

	n := rawNotice(t, doffin.Notice{ID: "2024-2", Heading: "Kontorrekvisita"})
	if _, err := s.UpsertNotice(ctx, n); err != nil {
		t.Fatalf("first upsert failed: %v", err)
	}

	result, err := s.UpsertNotice(ctx, n)
	if err != nil {
		t.Fatalf("second upsert failed: %v", err)
	}
	if result.Inserted {
		t.Error("expected Inserted=false on re-upsert")
	}
	if result.Changed {
		t.Error("expected Changed=false when raw_payload is unchanged")
	}

	var versionCount int
	if err := s.pool.QueryRow(ctx, `SELECT count(*) FROM notice_versions WHERE notice_id = $1`, n.ID).Scan(&versionCount); err != nil {
		t.Fatalf("querying notice_versions: %v", err)
	}
	if versionCount != 0 {
		t.Errorf("expected 0 notice_versions rows for unchanged payload, got %d", versionCount)
	}
}

func TestUpsertNotice_ChangedPayloadRecordsVersion(t *testing.T) {
	s := newTestStore(t)
	ctx := context.Background()

	n := rawNotice(t, doffin.Notice{ID: "2024-3", Heading: "Vintervedlikehold", Deadline: "2024-12-01T00:00:00Z"})
	if _, err := s.UpsertNotice(ctx, n); err != nil {
		t.Fatalf("first upsert failed: %v", err)
	}

	n2 := rawNotice(t, doffin.Notice{ID: "2024-3", Heading: "Vintervedlikehold (korrigert)", Deadline: "2024-12-15T00:00:00Z"})
	result, err := s.UpsertNotice(ctx, n2)
	if err != nil {
		t.Fatalf("second upsert failed: %v", err)
	}
	if result.Inserted {
		t.Error("expected Inserted=false on amendment")
	}
	if !result.Changed {
		t.Error("expected Changed=true when raw_payload differs")
	}

	var versionCount int
	if err := s.pool.QueryRow(ctx, `SELECT count(*) FROM notice_versions WHERE notice_id = $1`, n.ID).Scan(&versionCount); err != nil {
		t.Fatalf("querying notice_versions: %v", err)
	}
	if versionCount != 1 {
		t.Errorf("expected 1 notice_versions row after amendment, got %d", versionCount)
	}
}

// TestUpsertNotice_NeverTouchesCaseData is the key regression test: a case
// that staff have already started working (status, assignee) must survive a
// re-crawl of the same notice completely untouched.
func TestUpsertNotice_NeverTouchesCaseData(t *testing.T) {
	s := newTestStore(t)
	ctx := context.Background()

	n := rawNotice(t, doffin.Notice{ID: "2024-4", Heading: "Snørydding"})
	if _, err := s.UpsertNotice(ctx, n); err != nil {
		t.Fatalf("initial upsert failed: %v", err)
	}

	var userID string
	if err := s.pool.QueryRow(ctx, `
		INSERT INTO asp_net_users (user_name, normalized_user_name, display_name)
		VALUES ('carol', 'CAROL', 'Carol') RETURNING id`,
	).Scan(&userID); err != nil {
		t.Fatalf("seeding user: %v", err)
	}

	var caseID int64
	if err := s.pool.QueryRow(ctx, `
		INSERT INTO cases (notice_id, status, assignee_id) VALUES ($1, 'bidding', $2) RETURNING id`,
		n.ID, userID,
	).Scan(&caseID); err != nil {
		t.Fatalf("seeding case: %v", err)
	}
	if _, err := s.pool.Exec(ctx, `
		INSERT INTO case_comments (case_id, user_id, body) VALUES ($1, $2, 'Looks promising')`,
		caseID, userID,
	); err != nil {
		t.Fatalf("seeding comment: %v", err)
	}

	n2 := rawNotice(t, doffin.Notice{ID: "2024-4", Heading: "Snørydding (oppdatert frist)"})
	if _, err := s.UpsertNotice(ctx, n2); err != nil {
		t.Fatalf("re-upsert failed: %v", err)
	}

	var status, assignee string
	if err := s.pool.QueryRow(ctx, `SELECT status, assignee_id FROM cases WHERE id = $1`, caseID).Scan(&status, &assignee); err != nil {
		t.Fatalf("querying case: %v", err)
	}
	if status != "bidding" {
		t.Errorf("case status changed to %q after notice re-crawl, want unchanged 'bidding'", status)
	}
	if assignee != userID {
		t.Errorf("case assignee changed after notice re-crawl")
	}

	var commentCount int
	if err := s.pool.QueryRow(ctx, `SELECT count(*) FROM case_comments WHERE case_id = $1`, caseID).Scan(&commentCount); err != nil {
		t.Fatalf("querying comments: %v", err)
	}
	if commentCount != 1 {
		t.Errorf("expected 1 comment to survive re-crawl, got %d", commentCount)
	}
}
