-- Local development only. Do NOT run against production.
-- Seeds two staff users. Password for both is "ChangeMe123!" hashed with
-- ASP.NET Core Identity's default PasswordHasher<TUser> (v3 format) --
-- a real hash, generated and verified live via an actual login (see
-- docs/doffin-api-notes.md / session history), not a placeholder.
-- Regenerate if you change the seed password (see webapp README).

INSERT INTO asp_net_users (
    id, user_name, normalized_user_name, email, normalized_email,
    email_confirmed, password_hash, security_stamp, concurrency_stamp,
    lockout_enabled, display_name
) VALUES
    (gen_random_uuid(), 'alice', 'ALICE', 'alice@example.com', 'ALICE@EXAMPLE.COM',
     true, 'AQAAAAIAAYagAAAAEKuNhcjj6+FfKnO5FV3i8YL7CegUHw1Fpf6lADzpCmhqlkx/qksezPxTBoPNdDrQQQ==',
     gen_random_uuid()::text, gen_random_uuid()::text, true, 'Alice'),
    (gen_random_uuid(), 'bob', 'BOB', 'bob@example.com', 'BOB@EXAMPLE.COM',
     true, 'AQAAAAIAAYagAAAAEKuNhcjj6+FfKnO5FV3i8YL7CegUHw1Fpf6lADzpCmhqlkx/qksezPxTBoPNdDrQQQ==',
     gen_random_uuid()::text, gen_random_uuid()::text, true, 'Bob')
ON CONFLICT DO NOTHING;

INSERT INTO asp_net_user_roles (user_id, role_id)
SELECT u.id, r.id FROM asp_net_users u, asp_net_roles r
WHERE u.user_name IN ('alice', 'bob') AND r.normalized_name = 'STAFF'
ON CONFLICT DO NOTHING;

-- 'region' values are NUTS-style location codes, not display names --
-- NO081 is Oslo (confirmed live, see docs/doffin-api-notes.md).
INSERT INTO crawl_filters (filter_type, value) VALUES
    ('cpv', '72000000'),
    ('region', 'NO081')
ON CONFLICT DO NOTHING;
