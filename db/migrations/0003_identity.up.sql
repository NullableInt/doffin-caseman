-- Hand-written to match ASP.NET Core Identity's default EF Core mapping,
-- using snake_case names so both the crawler (SQL) and webapp (EF Core) agree
-- on one schema. See db/migrations/README (docs/doffin-api-notes.md) for context.

CREATE TABLE asp_net_users (
    id                     UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_name              TEXT NOT NULL,
    normalized_user_name   TEXT NOT NULL UNIQUE,
    email                  TEXT,
    normalized_email       TEXT,
    email_confirmed        BOOLEAN NOT NULL DEFAULT false,
    password_hash          TEXT,
    security_stamp         TEXT,
    concurrency_stamp      TEXT,
    phone_number           TEXT,
    phone_number_confirmed BOOLEAN NOT NULL DEFAULT false,
    two_factor_enabled     BOOLEAN NOT NULL DEFAULT false,
    lockout_end            TIMESTAMPTZ,
    lockout_enabled        BOOLEAN NOT NULL DEFAULT true,
    access_failed_count    INT NOT NULL DEFAULT 0,
    display_name           TEXT NOT NULL
);

CREATE TABLE asp_net_roles (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name              TEXT NOT NULL,
    normalized_name   TEXT NOT NULL UNIQUE,
    concurrency_stamp TEXT
);

CREATE TABLE asp_net_user_roles (
    user_id UUID NOT NULL REFERENCES asp_net_users(id) ON DELETE CASCADE,
    role_id UUID NOT NULL REFERENCES asp_net_roles(id) ON DELETE CASCADE,
    PRIMARY KEY (user_id, role_id)
);

-- Confirmed live: even plain username/password sign-in (no claims/external
-- logins ever added) fails without these four tables. SignInManager always
-- builds the claims principal via UserClaimsPrincipalFactory, which
-- unconditionally calls UserManager.GetClaimsAsync (and, for each role,
-- GetRoleClaimsAsync) -- querying user_claims/role_claims even when empty.
-- An earlier assumption that these could be skipped for v1 was wrong.
CREATE TABLE asp_net_user_claims (
    id          SERIAL PRIMARY KEY,
    user_id     UUID NOT NULL REFERENCES asp_net_users(id) ON DELETE CASCADE,
    claim_type  TEXT,
    claim_value TEXT
);

CREATE TABLE asp_net_role_claims (
    id          SERIAL PRIMARY KEY,
    role_id     UUID NOT NULL REFERENCES asp_net_roles(id) ON DELETE CASCADE,
    claim_type  TEXT,
    claim_value TEXT
);

CREATE TABLE asp_net_user_logins (
    login_provider        TEXT NOT NULL,
    provider_key          TEXT NOT NULL,
    provider_display_name TEXT,
    user_id               UUID NOT NULL REFERENCES asp_net_users(id) ON DELETE CASCADE,
    PRIMARY KEY (login_provider, provider_key)
);

CREATE TABLE asp_net_user_tokens (
    user_id        UUID NOT NULL REFERENCES asp_net_users(id) ON DELETE CASCADE,
    login_provider TEXT NOT NULL,
    name           TEXT NOT NULL,
    value          TEXT,
    PRIMARY KEY (user_id, login_provider, name)
);

INSERT INTO asp_net_roles (name, normalized_name, concurrency_stamp) VALUES ('Staff', 'STAFF', gen_random_uuid()::text);
