-- Lossy by necessity: a single-assignee schema can keep only one user per
-- case, so the earliest-assigned one is kept.
ALTER TABLE cases ADD COLUMN assignee_id UUID REFERENCES asp_net_users(id);
CREATE INDEX idx_cases_assignee ON cases (assignee_id);

UPDATE cases c
SET assignee_id = (
    SELECT a.user_id
    FROM case_assignees a
    WHERE a.case_id = c.id
    ORDER BY a.assigned_at, a.user_id
    LIMIT 1
);

DROP TABLE case_assignees;
