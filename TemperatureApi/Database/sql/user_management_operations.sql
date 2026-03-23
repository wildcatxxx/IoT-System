-- ============================================================================
-- Temperature API - User Management Operations
-- ============================================================================
-- Common SQL operations for user account management
-- Use these queries for development, testing, and maintenance
--
-- Database: PostgreSQL 15+
-- Created: 8 March 2026
-- ============================================================================

-- ============================================================================
-- 1. CREATE USER (Simulate Application Registration)
-- ============================================================================
-- Use this to manually create users in the database
-- Password must be BCrypt hashed (use bcrypt-cli or your application)

-- Example 1: Create a new user
-- USAGE: Replace values and password hash with actual BCrypt hash
-- INSERT INTO users (username, email, password_hash, created_at, is_active)
-- VALUES (
--     'john_doe',
--     'john@example.com',
--     '$2a$11$YOUR_BCRYPT_HASH_HERE',  -- Use actual BCrypt hash
--     NOW(),
--     true
-- )
-- ON CONFLICT (username) DO NOTHING;

-- ============================================================================
-- 2. READ/SELECT OPERATIONS
-- ============================================================================

-- List all users with full details
SELECT 
    id,
    username,
    email,
    created_at,
    last_login,
    is_active
FROM users
ORDER BY created_at DESC;

-- List active users only
SELECT 
    id,
    username,
    email,
    created_at,
    last_login
FROM users
WHERE is_active = true
ORDER BY username ASC;

-- List inactive users
SELECT 
    id,
    username,
    email,
    created_at,
    last_login,
    is_active
FROM users
WHERE is_active = false
ORDER BY created_at DESC;

-- Get user by username
-- SELECT * FROM users WHERE username = 'testuser';

-- Get user by email
-- SELECT * FROM users WHERE email = 'test@example.com';

-- Get user by ID
-- SELECT * FROM users WHERE id = 1;

-- Find users created in a date range
-- SELECT * FROM users 
-- WHERE created_at BETWEEN '2026-01-01' AND '2026-03-31'
-- ORDER BY created_at DESC;

-- Find users who never logged in
SELECT 
    id,
    username,
    email,
    created_at,
    is_active
FROM users
WHERE last_login IS NULL
ORDER BY created_at DESC;

-- Find users who haven't logged in for X days
-- SELECT 
--     id,
--     username,
--     email,
--     last_login,
--     CURRENT_TIMESTAMP - last_login as days_since_login
-- FROM users
-- WHERE last_login < CURRENT_TIMESTAMP - INTERVAL '30 days'
-- ORDER BY last_login DESC NULLS LAST;

-- Count statistics
SELECT 
    COUNT(*) as total_users,
    COUNT(CASE WHEN is_active = true THEN 1 END) as active_users,
    COUNT(CASE WHEN is_active = false THEN 1 END) as inactive_users,
    COUNT(CASE WHEN last_login IS NULL THEN 1 END) as never_logged_in
FROM users;

-- ============================================================================
-- 3. UPDATE OPERATIONS
-- ============================================================================

-- Update user's last login timestamp (done by application during login)
-- UPDATE users 
-- SET last_login = NOW()
-- WHERE id = 1;

-- Update user's last login by username
-- UPDATE users 
-- SET last_login = NOW()
-- WHERE username = 'testuser'
-- RETURNING id, username, last_login;

-- Update email address
-- UPDATE users 
-- SET email = 'newemail@example.com'
-- WHERE username = 'testuser'
-- WHERE NOT EXISTS (SELECT 1 FROM users WHERE email = 'newemail@example.com');

-- Update password hash (simulating password change)
-- UPDATE users 
-- SET password_hash = '$2a$11$NEW_BCRYPT_HASH_HERE'
-- WHERE username = 'testuser'
-- RETURNING username, created_at;

-- Disable user account (soft delete)
-- UPDATE users 
-- SET is_active = false
-- WHERE username = 'testuser'
-- RETURNING username, is_active;

-- Enable user account
-- UPDATE users 
-- SET is_active = true
-- WHERE username = 'testuser'
-- RETURNING username, is_active;

-- ============================================================================
-- 4. DELETE OPERATIONS (Use Carefully!)
-- ============================================================================

-- Hard delete single user (removes all data, also cascade deletes refresh tokens)
-- DELETE FROM users 
-- WHERE username = 'testuser'
-- RETURNING id, username;

-- Hard delete user by ID
-- DELETE FROM users 
-- WHERE id = 999
-- RETURNING id, username;

-- DELETE all inactive users (with confirmation)
-- DO $$
-- DECLARE
--     affected_count INT;
-- BEGIN
--     DELETE FROM users WHERE is_active = false;
--     GET DIAGNOSTICS affected_count = ROW_COUNT;
--     RAISE NOTICE 'Deleted % inactive users', affected_count;
-- END $$;

-- ============================================================================
-- 5. USEFUL AGGREGATION QUERIES
-- ============================================================================

-- Users by creation date (time series)
SELECT 
    DATE(created_at) as created_date,
    COUNT(*) as users_created
FROM users
GROUP BY DATE(created_at)
ORDER BY created_date DESC;

-- Most recently logged in users
SELECT 
    id,
    username,
    email,
    last_login,
    CURRENT_TIMESTAMP - last_login as time_since_login
FROM users
WHERE last_login IS NOT NULL
ORDER BY last_login DESC
LIMIT 10;

-- Account age analysis (in days)
SELECT 
    username,
    created_at,
    CURRENT_TIMESTAMP - created_at as account_age,
    EXTRACT(DAY FROM CURRENT_TIMESTAMP - created_at)::INT as days_old
FROM users
ORDER BY created_at DESC;

-- Username pattern search
-- SELECT id, username, email FROM users WHERE username ILIKE '%pattern%';

-- ============================================================================
-- 6. VALIDATION & INTEGRITY CHECKS
-- ============================================================================

-- Check for duplicate usernames (should return empty)
SELECT username, COUNT(*) as count
FROM users
GROUP BY username
HAVING COUNT(*) > 1
ORDER BY count DESC;

-- Check for duplicate emails (should return empty)
SELECT email, COUNT(*) as count
FROM users
GROUP BY email
HAVING COUNT(*) > 1
ORDER BY count DESC;

-- Check for invalid email format (basic check)
-- SELECT id, username, email FROM users 
-- WHERE email NOT LIKE '%@%.%';

-- Check for suspiciously short usernames (< 3 chars)
SELECT id, username, LENGTH(username) as name_length
FROM users
WHERE LENGTH(username) < 3;

-- Check for invalid password hashes (not BCrypt format)
-- BCrypt hashes start with $2a$, $2b$, or $2y$
SELECT id, username, SUBSTRING(password_hash, 1, 4) as hash_prefix
FROM users
WHERE password_hash NOT LIKE '$2%'
ORDER BY created_at DESC;

-- Find accounts with NULL timestamps (should be none)
SELECT id, username, created_at, last_login
FROM users
WHERE created_at IS NULL OR (is_active = true AND last_login IS NULL);

-- ============================================================================
-- 7. PERFORMANCE ANALYSIS
-- ============================================================================

-- Table size information
SELECT 
    'users' as table_name,
    pg_size_pretty(pg_total_relation_size('users')) as total_size,
    pg_size_pretty(pg_relation_size('users')) as table_size,
    pg_size_pretty(pg_total_relation_size('users') - pg_relation_size('users')) as indexes_size
;

-- Index usage statistics (requires stats collection)
-- SELECT 
--     indexrelname,
--     idx_scan,
--     idx_tup_read,
--     idx_tup_fetch
-- FROM pg_stat_user_indexes
-- WHERE relname = 'users'
-- ORDER BY idx_scan DESC;

-- Table row count
SELECT 
    COUNT(*) as total_rows,
    ROUND(AVG(pg_column_bytes(users.*)), 2) as avg_row_bytes
FROM users;

-- ============================================================================
-- 8. SECURITY AUDIT
-- ============================================================================

-- Find accounts that haven't been accessed
SELECT 
    username,
    email,
    created_at,
    CURRENT_TIMESTAMP - created_at as time_since_creation
FROM users
WHERE last_login IS NULL
ORDER BY created_at DESC;

-- Find accounts dormant for > 90 days
SELECT 
    username,
    email,
    last_login,
    CURRENT_TIMESTAMP - last_login as time_since_login,
    is_active
FROM users
WHERE last_login < CURRENT_TIMESTAMP - INTERVAL '90 days'
ORDER BY last_login DESC;

-- Active accounts created today
SELECT 
    id,
    username,
    email,
    created_at
FROM users
WHERE DATE(created_at) = CURRENT_DATE
AND is_active = true;

-- Recent logins (last 24 hours)
SELECT 
    username,
    email,
    last_login,
    is_active
FROM users
WHERE last_login > CURRENT_TIMESTAMP - INTERVAL '24 hours'
ORDER BY last_login DESC;

-- ============================================================================
-- 9. CLEANUP & MAINTENANCE
-- ============================================================================

-- Analyze table (updates statistics for query planner)
ANALYZE users;

-- Vacuum table (reclaim dead space)
VACUUM ANALYZE users;

-- Reindex all indexes on users table
-- REINDEX TABLE users;

-- Reset sequences (useful if you deleted users)
-- SELECT setval('users_id_seq', (SELECT MAX(id) FROM users) + 1);

-- ============================================================================
-- 10. TRANSACTION EXAMPLES
-- ============================================================================

-- Safe user creation with error handling
-- BEGIN TRANSACTION;
-- 
-- INSERT INTO users (username, email, password_hash, created_at, is_active)
-- VALUES (
--     'newuser',
--     'newuser@example.com',
--     '$2a$11$YOUR_BCRYPT_HASH_HERE',
--     NOW(),
--     true
-- );
-- 
-- -- Verify insert
-- SELECT * FROM users WHERE username = 'newuser';
-- 
-- COMMIT;  -- Save changes
-- -- Or ROLLBACK; to undo

-- Bulk operation with logging
-- BEGIN TRANSACTION;
-- 
-- UPDATE users 
-- SET last_login = NOW()
-- WHERE id IN (1, 2, 3);
-- 
-- SELECT * FROM users WHERE id IN (1, 2, 3);
-- 
-- COMMIT;

-- ============================================================================
-- 11. USEFUL PSQL COMMANDS (from command line)
-- ============================================================================
-- These are NOT SQL, run them from psql prompt (not in SQL script)

-- List all tables:
-- \dt

-- Describe users table:
-- \d users

-- List all indexes:
-- \di

-- View table with expanded output:
-- \x SELECT * FROM users;

-- Show query plan:
-- EXPLAIN SELECT * FROM users WHERE username = 'testuser';

-- Show query plan with execution:
-- EXPLAIN ANALYZE SELECT * FROM users WHERE username = 'testuser';

-- ============================================================================
-- 12. BACKUP COMMANDS (from shell, not in psql)
-- ============================================================================
-- Run these from terminal/bash/zsh, not from SQL prompt

-- Backup just the users table
-- pg_dump -h localhost -U postgres -d iot_db -t users > users_backup.sql

-- Backup with data and structure separately
-- pg_dump -h localhost -U postgres -d iot_db -t users --schema-only > users_schema.sql
-- pg_dump -h localhost -U postgres -d iot_db -t users --data-only > users_data.sql

-- Restore from backup
-- psql -h localhost -U postgres -d iot_db < users_backup.sql

-- Backup and compress
-- pg_dump -h localhost -U postgres -d iot_db -t users | gzip > users_backup.sql.gz

-- ============================================================================
-- END OF MANAGEMENT OPERATIONS
-- ============================================================================
