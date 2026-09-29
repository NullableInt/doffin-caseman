# Doffin Case Management

Tracks public procurement notices from [doffin.no](https://doffin.no) relevant
to the business and manages the internal workflow of deciding whether/how to
bid on them.

Three components, run together via Docker Compose:

- **`crawler/`** (Go) -- runs daily, pulls notices matching configured CPV
  codes / regions from the Doffin Public API, stores them in Postgres.
- **`db/migrations/`** (plain SQL, applied via `golang-migrate`) -- the single
  source of truth for the schema; neither app owns migrations.
- **`webapp/`** (.NET 10, ASP.NET Core Blazor Server) -- where staff view
  notices, work cases through a status pipeline, assign them, and comment.

See `/home/moira/.claude/plans/we-are-building-a-pure-rossum.md` for the full
design rationale, and `docs/doffin-api-notes.md` for what's confirmed vs.
assumed about the Doffin API.

## Prerequisites

- Docker, or **Podman** (verified working setup: `podman` + `podman-compose`,
  installed via `brew install podman-compose`; `podman compose ...` then
  works as a thin wrapper). All image references in this repo are
  fully-qualified (`docker.io/library/postgres:...`, not bare
  `postgres:...`) because Podman's short-name resolution refuses to guess a
  registry outside a TTY and hangs/fails otherwise -- keep new image refs
  fully-qualified too.
- On SELinux-enforcing hosts (Fedora/Bazzite default), bind-mounted volumes
  need a `:Z` relabel suffix, already applied where needed in
  `docker-compose.yml`.
- A Doffin Public API subscription key: sign up at
  https://dof-notices-prod-api.developer.azure-api.net/ (actual API calls go
  to `api.doffin.no`, a different host -- see `docs/doffin-api-notes.md`).
- For local (non-container) development: Go 1.23+, .NET 10 SDK (verified
  working via `brew install dotnet`, which is keg-only -- add
  `export DOTNET_ROOT="$(brew --prefix dotnet)/libexec"` and
  `export PATH="$(brew --prefix dotnet)/bin:$PATH"` to your shell profile)

## Secrets

This repo is public. `POSTGRES_PASSWORD` and `DOFFIN_API_SUBSCRIPTION_KEY`
only ever live in `.env`, which is gitignored and must never be committed --
`.env.example` documents the variable names with empty/placeholder values
only.

After cloning, enable the repo's pre-commit safeguard (it's tracked under
`.githooks/`, not `.git/hooks/`, so this is a one-time step per clone):

```sh
git config core.hooksPath .githooks
```

It refuses any commit that stages a file literally named `.env`, or a line
in a config-shaped file (`.env*`, `.yml`, `.json`, `.sql`, ...) that looks
like a real secret value assigned to a `*_KEY`/`*_PASSWORD`/`*_SECRET`/
`*_TOKEN` variable. It does not scan source code, since Go/C# legitimately
reference these variable names as string literals without ever assigning a
real value to them.

## Running the stack

```sh
cp .env.example .env
# edit .env: set POSTGRES_PASSWORD, DOFFIN_API_SUBSCRIPTION_KEY,
# DOFFIN_CPV_CODES and/or DOFFIN_REGIONS

docker compose up --build   # or: podman compose up --build
```

This starts Postgres, applies all migrations, then starts the crawler
(which runs an immediate crawl on startup, then on `CRAWL_SCHEDULE_CRON`)
and the webapp at http://localhost:8080.

`docker-compose.override.yml` is applied automatically in this directory and
adds a host-exposed Postgres port (`5432`) for local `psql`/GUI access.

## Seeding a login user

The webapp has no self-service registration (internal tool). For local dev,
`db/seed/dev_seed.sql` creates two users (`alice`, `bob`) -- **but ships with
a placeholder password hash that will not authenticate.** Generate a real one
with a throwaway script and swap it into the seed file before running it:

Temporarily add this as the first line of `webapp/src/DoffinCaseman.Web/Program.cs`,
run `dotnet run --project src/DoffinCaseman.Web`, copy the printed hash, then
remove the line:

```csharp
Console.WriteLine(new PasswordHasher<ApplicationUser>().HashPassword(null!, "ChangeMe123!")); return;
```

Then apply the seed:

```sh
psql "$DATABASE_URL" -f db/seed/dev_seed.sql
```

For production, seed users the same way (via SQL insert with a real password
hash) -- do not enable self-registration.

## Development without Docker

**Crawler:**
```sh
cd crawler
go build ./...
go vet ./...
go test ./...        # DB-backed tests in internal/store skip automatically
                      # without a Docker/Podman daemon
```

Under rootless Podman, testcontainers-go needs to be pointed at the Podman
socket explicitly (Ryuk, its cleanup sidecar, is also unreliable rootless --
disable it):

```sh
export DOCKER_HOST="unix:///run/user/$(id -u)/podman/podman.sock"
export TESTCONTAINERS_RYUK_DISABLED=true
go test ./...
```

**Webapp:**
```sh
cd webapp
dotnet build
dotnet test           # requires Docker/Podman for testcontainers
dotnet run --project src/DoffinCaseman.Web
```

## Verifying end-to-end

1. `docker compose up --build` (or `podman compose up --build`); confirm
   `migrate` exits 0 and the other three services report healthy/running.
2. `docker compose logs crawler`; confirm a crawl ran and upserted notices
   matching your configured CPV/region filters.
3. `psql` into Postgres (`localhost:5432` via the dev override) and spot
   check: `SELECT notice_id, title, cpv_codes, region_codes FROM notices LIMIT 5;`
4. Open http://localhost:8080, log in, confirm the notice list shows crawled
   notices and the filter bar works.
5. Click "Handle" on a notice, walk its case through the full status
   pipeline, add comments as two different users, confirm attribution.
6. Re-run the crawler and confirm `last_seen_at` updates on existing notices
   while case status/assignee/comments are untouched -- this is the key
   regression the Go tests in `crawler/internal/store/notices_test.go` and
   the .NET tests in
   `webapp/tests/DoffinCaseman.Web.Tests/CaseWorkflowTests.cs` guard against.
