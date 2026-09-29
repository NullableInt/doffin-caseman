CREATE TYPE case_status AS ENUM (
    'new', 'under_review', 'bidding', 'submitted', 'won', 'lost', 'archived'
);

CREATE TABLE cases (
    id          BIGSERIAL PRIMARY KEY,
    notice_id   TEXT NOT NULL UNIQUE REFERENCES notices(notice_id),
    status      case_status NOT NULL DEFAULT 'new',
    assignee_id UUID REFERENCES asp_net_users(id),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_cases_status   ON cases (status);
CREATE INDEX idx_cases_assignee ON cases (assignee_id);

CREATE TABLE case_status_history (
    id         BIGSERIAL PRIMARY KEY,
    case_id    BIGINT NOT NULL REFERENCES cases(id) ON DELETE CASCADE,
    old_status case_status,
    new_status case_status NOT NULL,
    changed_by UUID REFERENCES asp_net_users(id),
    changed_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_case_status_history_case_id ON case_status_history (case_id, changed_at);

CREATE TABLE case_comments (
    id                BIGSERIAL PRIMARY KEY,
    case_id           BIGINT NOT NULL REFERENCES cases(id) ON DELETE CASCADE,
    user_id           UUID NOT NULL REFERENCES asp_net_users(id),
    body              TEXT NOT NULL,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    parent_comment_id BIGINT REFERENCES case_comments(id)
);

CREATE INDEX idx_case_comments_case_id ON case_comments (case_id, created_at);
