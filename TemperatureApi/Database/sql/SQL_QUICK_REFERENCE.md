-- ============================================================================
-- SQL SCRIPTS QUICK REFERENCE
-- ============================================================================
-- Quick guide to the SQL scripts for user table management
--
-- Files in sql/ directory:
--   1. users_table.sql - Table creation and schema setup
--   2. user_management_operations.sql - Common queries and operations
--
-- Created: 8 March 2026
-- ============================================================================

-- ============================================================================
-- FILE 1: users_table.sql
-- ============================================================================
-- PURPOSE: Create the users table and all related database objects

-- Contents:
-- ✓ Table creation with all columns and constraints
-- ✓ Indexes for performance (3 indexes)
-- ✓ CHECK constraints for validation
-- ✓ Test data (testuser and demo users)
-- ✓ Views (active_users, users_with_activity)
-- ✓ Audit table and triggers for change tracking
-- ✓ Permission grants (commented, for multi-user setups)
-- ✓ Useful queries and maintenance tasks

-- How to run:
-- $ psql -h localhost -U postgres -d iot_db -f sql/users_table.sql

-- Or from psql prompt:
-- \i sql/users_table.sql

-- Expected output:
-- CREATE TABLE
-- CREATE INDEX (3x)
-- ALTER TABLE (3x)
-- INSERT (2x)
-- CREATE VIEW (2x)
-- CREATE TABLE
-- CREATE INDEX (2x)
-- CREATE FUNCTION
-- CREATE TRIGGER

-- ============================================================================
-- FILE 2: user_management_operations.sql
-- ============================================================================
-- PURPOSE: Collection of useful queries for managing users

-- Contents:
-- ✓ Create user examples
-- ✓ Read/Select operations (10+ query examples)
-- ✓ Update operations (6 operations)
-- ✓ Delete operations (3 operations)
-- ✓ Aggregation queries (4 queries)
-- ✓ Validation & integrity checks (7 checks)
-- ✓ Performance analysis queries
-- ✓ Security audit queries
-- ✓ Cleanup & maintenance operations
-- ✓ Transaction examples
-- ✓ PSQL command reference
-- ✓ Backup commands

-- How to run:
-- Most queries are commented (preceded by --)
-- Uncomment the query you want to run and execute

-- Or run all queries to see results:
-- $ psql -h localhost -U postgres -d iot_db -f sql/user_management_operations.sql

-- ============================================================================
-- QUICK SETUP INSTRUCTIONS
-- ============================================================================

-- 1. CREATE USERS TABLE (first time setup)
--    $ psql -h localhost -U postgres -d iot_db -f sql/users_table.sql

-- 2. VERIFY SETUP
--    $ psql -h localhost -U postgres -d iot_db
--    > SELECT COUNT(*) FROM users;
--    > SELECT * FROM active_users;
--    > \d users

-- 3. RUN MANAGEMENT QUERIES
--    $ psql -h localhost -U postgres -d iot_db -f sql/user_management_operations.sql

-- ============================================================================
-- COMMON TASKS & QUICK COMMANDS
-- ============================================================================

-- Task: Check all users
-- SELECT id, username, email, created_at, is_active FROM users;

-- Task: Get test user details
-- SELECT * FROM users WHERE username = 'testuser';

-- Task: Add new user (from application is preferred)
-- INSERT INTO users (username, email, password_hash, created_at, is_active)
-- VALUES ('newuser', 'new@example.com', '$2a$11$HASH', NOW(), true);

-- Task: Disable user
-- UPDATE users SET is_active = false WHERE username = 'username';

-- Task: Reset last_login for user
-- UPDATE users SET last_login = NULL WHERE username = 'username';

-- Task: Delete user (use with caution!)
-- DELETE FROM users WHERE username = 'username';

-- Task: View audit trail
-- SELECT * FROM users_audit ORDER BY changed_at DESC LIMIT 20;

-- Task: Count users by status
-- SELECT is_active, COUNT(*) FROM users GROUP BY is_active;

-- Task: Check for duplicate usernames
-- SELECT username, COUNT(*) FROM users GROUP BY username HAVING COUNT(*) > 1;

-- ============================================================================
-- INSTALLATION CHECKLIST
-- ============================================================================

-- [ ] PostgreSQL installed and running
-- [ ] Database 'iot_db' created
-- [ ] sql/ directory exists
-- [ ] users_table.sql file copied to sql/ directory
-- [ ] Run: psql -h localhost -U postgres -d iot_db -f sql/users_table.sql
-- [ ] Verify: SELECT COUNT(*) FROM users;
-- [ ] Check test data: SELECT * FROM users WHERE username = 'testuser';
-- [ ] Application can connect to database
-- [ ] Application can authenticate with testuser
-- [ ] Run test-auth.sh to verify end-to-end flow

-- ============================================================================
-- COLUMN REFERENCE
-- ============================================================================

-- Column: id
-- Type: SERIAL (auto-incrementing integer)
-- Usage: Primary key, unique identifier
-- Example: 1, 2, 3

-- Column: username
-- Type: VARCHAR(255) UNIQUE NOT NULL
-- Usage: Login identifier
-- Constraints: Unique, not null, 3-20 characters
-- Example: testuser, john_doe, admin_user

-- Column: email
-- Type: VARCHAR(255) UNIQUE NOT NULL
-- Usage: Email address
-- Constraints: Unique, not null, valid email format
-- Example: test@example.com

-- Column: password_hash
-- Type: VARCHAR(255) NOT NULL
-- Usage: BCrypt hashed password (NEVER plain text)
-- Format: $2a$11$...(59 characters total)
-- Example: $2a$11$Fwd3h9gPGwKJMTNwvJnJG.Eyt6BXQRWXpjqgE9aFQsglKN4TlM6pO

-- Column: created_at
-- Type: TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
-- Usage: Account creation timestamp
-- Auto-set: Yes (set when row is inserted)
-- Example: 2026-03-08 10:30:45.123

-- Column: last_login
-- Type: TIMESTAMP (nullable)
-- Usage: Last login timestamp
-- Auto-set: No (updated by application)
-- Example: 2026-03-08 15:22:30.456, or NULL if never logged in

-- Column: is_active
-- Type: BOOLEAN NOT NULL DEFAULT true
-- Usage: Account active status
-- Values: true (active), false (disabled)
-- Example: true, false

-- ============================================================================
-- INDEX REFERENCE
-- ============================================================================

-- Index: idx_users_username
-- Table: users
-- Columns: username
-- Purpose: Fast username lookups during login
-- Used by: AuthenticationService.LoginAsync, UserRepository.GetUserByUsernameAsync
-- Query speed: ~1ms (with index) vs ~100ms (full table scan)

-- Index: idx_users_is_active
-- Table: users
-- Columns: is_active
-- Purpose: Fast filtering of active users
-- Used by: User management queries, account validation
-- Query speed: ~1ms (with index) vs ~100ms (full table scan)

-- Index: idx_users_username_active
-- Table: users
-- Columns: username, is_active
-- Purpose: Combined lookup for active users by username
-- Used by: Login validation, account lookup
-- Query speed: Optimized for WHERE username = ? AND is_active = true

-- ============================================================================
-- SECURITY CONSIDERATIONS
-- ============================================================================

-- ✓ Passwords are BCrypt hashed (11 rounds)
-- ✓ Usernames and emails are unique (no duplicates)
-- ✓ Passwords NEVER stored in plain text
-- ✓ Last login tracked for security monitoring
-- ✓ Soft delete via is_active flag (preserves data)
-- ✓ Audit trail available via audit table and triggers
-- ✓ Created_at is immutable (set by database)
-- ✓ Timestamps are in UTC (CURRENT_TIMESTAMP)

-- Security best practices:
-- 1. Always hash passwords with BCrypt before storing
-- 2. Never expose password_hash in API responses
-- 3. Use parameterized queries to prevent SQL injection
-- 4. Monitor users_audit table for suspicious activity
-- 5. Implement rate limiting on login attempts
-- 6. Log all authentication events
-- 7. Use HTTPS for all password transmissions
-- 8. Implement account lockout after failed attempts
-- 9. Require strong passwords (8+ chars, mixed case, numbers, special chars)
-- 10. Use JWT tokens with short expiration (15 minutes)

-- ============================================================================
-- TROUBLESHOOTING
-- ============================================================================

-- Issue: "ERROR: duplicate key value violates unique constraint"
-- Cause: Username or email already exists
-- Solution: Use a different username/email or check existing users:
--   SELECT username, email FROM users WHERE username = 'your_username';

-- Issue: "ERROR: relation 'users' does not exist"
-- Cause: Table not created yet
-- Solution: Run users_table.sql first:
--   psql -h localhost -U postgres -d iot_db -f sql/users_table.sql

-- Issue: "ERROR: invalid byte sequence for encoding UTF8"
-- Cause: Invalid character encoding
-- Solution: Ensure file is UTF-8 encoded and database is UTF-8:
--   file -I sql/users_table.sql

-- Issue: "ERROR: connection refused"
-- Cause: PostgreSQL not running
-- Solution: Start PostgreSQL:
--   docker-compose up -d postgres
--   or: brew services start postgresql

-- Issue: "ERROR: database 'iot_db' does not exist"
-- Cause: Database not created
-- Solution: Create database first:
--   createdb -U postgres iot_db

-- Issue: Authentication fails with BCrypt hash mismatch
-- Cause: Password not hashed correctly or hash format invalid
-- Solution: Verify hash format starts with $2a$, $2b$, or $2y$:
--   SELECT password_hash FROM users WHERE username = 'testuser';

-- ============================================================================
-- RELATED DOCUMENTATION
-- ============================================================================

-- File: AUTHENTICATION.md
-- Purpose: Complete authentication system documentation
-- Contents: API endpoints, JWT details, examples, troubleshooting

-- File: AUTH_QUICK_REFERENCE.md
-- Purpose: 5-minute setup guide
-- Contents: Quick start, basic commands, testing

-- File: SETUP_AND_TEST.md
-- Purpose: End-to-end setup and testing guide
-- Contents: Docker setup, Swagger testing, curl examples

-- File: AuthenticationService.cs
-- Purpose: C# authentication service implementation
-- Usage: Registration, login, token refresh, validation

-- File: UserRepository.cs
-- Purpose: C# user data access layer
-- Usage: Database queries for user operations

-- File: test-auth.sh
-- Purpose: Automated authentication testing script
-- Usage: Full end-to-end testing from registration to logout

-- ============================================================================
-- VERSION HISTORY
-- ============================================================================

-- Version 1.0 - March 8, 2026
-- - Initial creation of users_table.sql
-- - Complete schema with indexes and constraints
-- - Audit table and triggers
-- - Test data included
-- - user_management_operations.sql with 50+ query examples
-- - This quick reference guide

-- ============================================================================
-- END OF QUICK REFERENCE
-- ============================================================================
