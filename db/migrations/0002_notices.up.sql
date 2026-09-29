CREATE TABLE notices (
    id                  BIGSERIAL PRIMARY KEY,
    notice_id           TEXT NOT NULL UNIQUE,
    title               TEXT NOT NULL,
    buyer_name          TEXT,
    description         TEXT,
    notice_type         TEXT,
    status              TEXT,
    cpv_codes           TEXT[] NOT NULL DEFAULT '{}',
    -- NUTS-style location codes (e.g. "NO081"), as returned by the Doffin
    -- search API's "locationId" array -- a notice can span multiple regions,
    -- so this is an array like cpv_codes, not a scalar.
    region_codes        TEXT[] NOT NULL DEFAULT '{}',
    published_date      TIMESTAMPTZ,
    deadline            TIMESTAMPTZ,
    contract_value_nok  NUMERIC(18,2),
    raw_payload         JSONB NOT NULL,
    first_seen_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_changed_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_notices_cpv_codes   ON notices USING GIN (cpv_codes);
CREATE INDEX idx_notices_region      ON notices USING GIN (region_codes);
CREATE INDEX idx_notices_status      ON notices (status);
CREATE INDEX idx_notices_published   ON notices (published_date);
CREATE INDEX idx_notices_deadline    ON notices (deadline);
CREATE INDEX idx_notices_raw_payload ON notices USING GIN (raw_payload);

CREATE TABLE notice_versions (
    id           BIGSERIAL PRIMARY KEY,
    notice_id    TEXT NOT NULL REFERENCES notices(notice_id) ON DELETE CASCADE,
    captured_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    raw_payload  JSONB NOT NULL,
    diff_summary TEXT
);

CREATE INDEX idx_notice_versions_notice_id ON notice_versions (notice_id, captured_at DESC);
