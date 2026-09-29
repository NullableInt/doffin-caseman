CREATE TABLE crawl_filters (
    id          SERIAL PRIMARY KEY,
    filter_type TEXT NOT NULL CHECK (filter_type IN ('cpv', 'region')),
    value       TEXT NOT NULL,
    enabled     BOOLEAN NOT NULL DEFAULT true,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (filter_type, value)
);
