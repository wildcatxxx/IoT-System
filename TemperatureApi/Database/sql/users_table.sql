-- ============================================================================
-- Temperature API - User Management SQL Script
-- ============================================================================
-- This script creates the users table and related database objects for
-- the Temperature API authentication system.
--
-- Database: PostgreSQL 15+
-- Created: 8 March 2026
-- ============================================================================

-- ============================================================================
-- STEP 1: Create USERS TABLE
-- ============================================================================
-- The users table stores user account information including authentication
-- credentials and account status.

CREATE TABLE IF NOT EXISTS users (
    -- Primary Key
    id SERIAL PRIMARY KEY,
    
    -- Authentication Credentials
    username VARCHAR(255) UNIQUE NOT NULL,
        -- Unique username for login
        -- Length: 3-20 characters
        -- Characters: alphanumeric + underscore only
        -- Validation: enforced at application level
    
    email VARCHAR(255) UNIQUE NOT NULL,
        -- Unique email address
        -- Used for account recovery and notifications
        -- Format: valid email (enforced at application level)
    
    password_hash VARCHAR(255) NOT NULL,
        -- BCrypt hashed password
        -- Never store plain text passwords
        -- Hash cost: 11 rounds (BCrypt default)
        -- Example hash: $2a$11$Fwd3h9gPGwKJMTNwvJnJG...
    
    -- Timestamps
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
        -- When the user account was created
        -- Set automatically by database
        -- Used for audit trail
    
    last_login TIMESTAMP,
        -- When the user last logged in
        -- Updated on successful login
        -- Can be NULL if user never logged in
        -- Used for analytics and security
    
    -- Account Status
    is_active BOOLEAN NOT NULL DEFAULT true
        -- Whether the account is active
        -- true = account is enabled
        -- false = account is disabled/suspended
        -- Allows soft delete without removing data
);

-- ============================================================================
-- STEP 2: Create INDEXES for Performance
-- ============================================================================
-- Indexes optimize query performance for frequently accessed columns

-- Index for username lookups (used during login)
CREATE INDEX IF NOT EXISTS idx_users_username ON users(username);
    -- Optimizes: SELECT * FROM users WHERE username = '...'
    -- Used by: LoginAsync, GetUserByUsernameAsync

-- Index for active user filtering
CREATE INDEX IF NOT EXISTS idx_users_is_active ON users(is_active);
    -- Optimizes: SELECT * FROM users WHERE is_active = true
    -- Used by: Authentication validation, user management

-- Composite index for common queries
CREATE INDEX IF NOT EXISTS idx_users_username_active 
    ON users(username, is_active);
    -- Optimizes: SELECT * FROM users WHERE username = '...' AND is_active = true
    -- Used by: Login queries, active user lookup

-- ============================================================================
-- STEP 3: Create CONSTRAINTS & VALIDATION
-- ============================================================================
-- Additional constraints ensure data integrity

-- NOT NULL constraints on critical fields
-- Already defined above (username, email, password_hash, created_at, is_active)

-- UNIQUE constraints
-- Already defined above (username, email)

-- CHECK constraints (optional, for additional validation)
-- Uncomment if you want database-level validation

-- Ensure username is not empty
ALTER TABLE users 
    ADD CONSTRAINT check_username_not_empty 
    CHECK (LENGTH(TRIM(username)) > 0)
    IF NOT EXISTS;

-- Ensure email is not empty
ALTER TABLE users 
    ADD CONSTRAINT check_email_not_empty 
    CHECK (LENGTH(TRIM(email)) > 0)
    IF NOT EXISTS;

-- Ensure password hash is substantial
ALTER TABLE users 
    ADD CONSTRAINT check_password_hash_length 
    CHECK (LENGTH(TRIM(password_hash)) >= 20)
    IF NOT EXISTS;

-- ============================================================================
-- STEP 4: Insert TEST DATA
-- ============================================================================
-- Test user for development and testing
-- Password: Test123! (BCrypt hash with 11 rounds)
-- Use these credentials to test the authentication system

INSERT INTO users (username, email, password_hash, created_at, is_active)
SELECT 
    'testuser',
    'test@example.com',
    '$2a$11$Fwd3h9gPGwKJMTNwvJnJG.Eyt6BXQRWXpjqgE9aFQsglKN4TlM6pO',
    NOW(),
    true
WHERE NOT EXISTS (SELECT 1 FROM users WHERE username = 'testuser')
ON CONFLICT (username) DO NOTHING;

-- Additional test users (optional)
INSERT INTO users (username, email, password_hash, created_at, is_active)
SELECT 
    'demo',
    'demo@example.com',
    '$2a$11$Fwd3h9gPGwKJMTNwvJnJG.Eyt6BXQRWXpjqgE9aFQsglKN4TlM6pO',
    NOW(),
    true
WHERE NOT EXISTS (SELECT 1 FROM users WHERE username = 'demo')
ON CONFLICT (username) DO NOTHING;

-- ============================================================================
-- STEP 5: Create VIEWS (Optional)
-- ============================================================================
-- Views provide convenient ways to query user data

-- Active users view
CREATE OR REPLACE VIEW active_users AS
SELECT 
    id,
    username,
    email,
    created_at,
    last_login
FROM users
WHERE is_active = true
ORDER BY created_at DESC;

-- Users with last login info
CREATE OR REPLACE VIEW users_with_activity AS
SELECT 
    id,
    username,
    email,
    created_at,
    last_login,
    CASE 
        WHEN last_login IS NULL THEN 'Never'
        ELSE TO_CHAR(last_login, 'YYYY-MM-DD HH24:MI:SS')
    END AS last_login_formatted,
    is_active
FROM users
ORDER BY last_login DESC NULLS LAST;

-- ============================================================================
-- STEP 6: Create AUDIT TRIGGERS (Optional)
-- ============================================================================
-- Triggers automatically track changes to users table

-- Create audit table to log all changes
CREATE TABLE IF NOT EXISTS users_audit (
    audit_id SERIAL PRIMARY KEY,
    user_id INTEGER,
    action VARCHAR(50) NOT NULL, -- INSERT, UPDATE, DELETE
    old_values JSONB,
    new_values JSONB,
    changed_by VARCHAR(255),
    changed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE SET NULL
);

-- Index on audit table for performance
CREATE INDEX IF NOT EXISTS idx_users_audit_user_id ON users_audit(user_id);
CREATE INDEX IF NOT EXISTS idx_users_audit_changed_at ON users_audit(changed_at DESC);

-- Create audit trigger function
CREATE OR REPLACE FUNCTION audit_users_changes()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO users_audit (user_id, action, new_values, changed_by, changed_at)
        VALUES (NEW.id, 'INSERT', ROW_TO_JSON(NEW), CURRENT_USER, CURRENT_TIMESTAMP);
    ELSIF TG_OP = 'UPDATE' THEN
        INSERT INTO users_audit (user_id, action, old_values, new_values, changed_by, changed_at)
        VALUES (NEW.id, 'UPDATE', ROW_TO_JSON(OLD), ROW_TO_JSON(NEW), CURRENT_USER, CURRENT_TIMESTAMP);
    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO users_audit (user_id, action, old_values, changed_by, changed_at)
        VALUES (OLD.id, 'DELETE', ROW_TO_JSON(OLD), CURRENT_USER, CURRENT_TIMESTAMP);
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- Attach trigger to users table
DROP TRIGGER IF EXISTS users_audit_trigger ON users;
CREATE TRIGGER users_audit_trigger
AFTER INSERT OR UPDATE OR DELETE ON users
FOR EACH ROW
EXECUTE FUNCTION audit_users_changes();

-- ============================================================================
-- STEP 7: Grant PERMISSIONS (for multi-user databases)
-- ============================================================================
-- Set appropriate permissions for different roles

-- Example: Create application role (uncomment and adjust as needed)
-- DO
-- $$
-- BEGIN
--     IF NOT EXISTS (SELECT FROM pg_user WHERE usename = 'app_user') THEN
--         CREATE ROLE app_user LOGIN PASSWORD 'strong_password_here';
--     END IF;
-- END
-- $$;

-- Grant SELECT on users table
-- GRANT SELECT ON users TO app_user;
-- GRANT INSERT ON users TO app_user;
-- GRANT UPDATE ON users TO app_user;
-- GRANT DELETE ON users TO app_user;

-- Grant sequence permissions (for SERIAL columns)
-- GRANT USAGE, SELECT ON SEQUENCE users_id_seq TO app_user;

-- ============================================================================
-- STEP 8: Useful Queries
-- ============================================================================
-- Query Examples for Testing and Maintenance

-- Get all users with login info
-- SELECT id, username, email, created_at, last_login, is_active FROM users;

-- Get active users only
-- SELECT * FROM active_users;

-- Count users
-- SELECT COUNT(*) as total_users FROM users;
-- SELECT COUNT(*) as active_users FROM users WHERE is_active = true;

-- Find users by email
-- SELECT * FROM users WHERE email LIKE '%example.com%';

-- Check password hash for a user
-- SELECT id, username, password_hash FROM users WHERE username = 'testuser';

-- Update last login
-- UPDATE users SET last_login = NOW() WHERE id = 1;

-- Disable user account
-- UPDATE users SET is_active = false WHERE username = 'username';

-- View audit history
-- SELECT * FROM users_audit WHERE user_id = 1 ORDER BY changed_at DESC;

-- ============================================================================
-- STEP 9: Maintenance Tasks
-- ============================================================================
-- Regular maintenance queries

-- Analyze table for query optimization
-- ANALYZE users;

-- Vacuum to reclaim space
-- VACUUM ANALYZE users;

-- Check table size
-- SELECT 
--     schemaname,
--     tablename,
--     pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size
-- FROM pg_tables 
-- WHERE tablename = 'users';

-- Check indexes
-- SELECT indexname, indexdef FROM pg_indexes WHERE tablename = 'users';

-- ============================================================================
-- STEP 10: Backup & Recovery
-- ============================================================================
-- Backup commands (run from shell, not in SQL)

-- Backup users table only
-- pg_dump -U postgres -d iot_db -t users > users_backup.sql

-- Backup entire database
-- pg_dump -U postgres -d iot_db > iot_db_backup.sql

-- Restore from backup
-- psql -U postgres -d iot_db < users_backup.sql

-- ============================================================================
-- VERIFICATION QUERIES
-- ============================================================================
-- Run these to verify the setup

-- Verify table structure
-- \d users;

-- List all columns
-- SELECT column_name, data_type, is_nullable FROM information_schema.columns 
-- WHERE table_name = 'users' ORDER BY ordinal_position;

-- List all indexes
-- \di idx_users*;

-- List all views
-- \dv *users*;

-- List all triggers
-- \dT+ users_audit_trigger;

-- ============================================================================
-- END OF SCRIPT
-- ============================================================================
-- For support, see: AUTHENTICATION.md
-- For testing, see: SETUP_AND_TEST.md
-- ============================================================================
