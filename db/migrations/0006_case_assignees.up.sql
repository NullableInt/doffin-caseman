-- A case can have several assignees. Replaces the single cases.assignee_id.
CREATE TABLE case_assignees (
    case_id     BIGINT NOT NULL REFERENCES cases(id) ON DELETE CASCADE,
    user_id     UUID   NOT NULL REFERENCES asp_net_users(id),
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (case_id, user_id)
);

-- "My cases" looks cases up by user.
CREATE INDEX idx_case_assignees_user ON case_assignees (user_id);

INSERT INTO case_assignees (case_id, user_id, assigned_at)
SELECT id, assignee_id, updated_at
FROM cases
WHERE assignee_id IS NOT NULL;

DROP INDEX idx_cases_assignee;
ALTER TABLE cases DROP COLUMN assignee_id;
