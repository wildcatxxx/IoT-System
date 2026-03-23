-- Users table
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(255) UNIQUE NOT NULL,
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login TIMESTAMP,
    is_active BOOLEAN NOT NULL DEFAULT true
);

-- Refresh tokens table
CREATE TABLE IF NOT EXISTS refresh_tokens (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token TEXT NOT NULL UNIQUE,
    expires_at TIMESTAMP NOT NULL,
    is_valid BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    invalidated_at TIMESTAMP
);

-- Temperatures table (existing)
CREATE TABLE IF NOT EXISTS temperatures (
    id SERIAL PRIMARY KEY,
    value NUMERIC(5, 2) NOT NULL,
    recorded_at TIMESTAMP NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS idx_users_username ON users(username);
CREATE INDEX IF NOT EXISTS idx_users_is_active ON users(is_active);
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_user_id ON refresh_tokens(user_id);
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_token ON refresh_tokens(token);
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_expires_at ON refresh_tokens(expires_at);
CREATE INDEX IF NOT EXISTS idx_temperatures_recorded_at ON temperatures(recorded_at DESC);

-- Insert a test user (password: 'Test123!')
-- In production, create users through the API
INSERT INTO users (username, email, password_hash, created_at, is_active)
SELECT 'testuser', 'test@example.com', '$2a$11$Fwd3h9gPGwKJMTNwvJnJG.Eyt6BXQRWXpjqgE9aFQsglKN4TlM6pO', NOW(), true
WHERE NOT EXISTS (SELECT 1 FROM users WHERE username = 'testuser');
