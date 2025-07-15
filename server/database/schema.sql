-- Database schema for the Steam authentication system
-- Run this SQL script in your PostgreSQL database

-- Users table
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    steam_id VARCHAR(20) UNIQUE NOT NULL,
    username VARCHAR(255) NOT NULL,
    avatar_url TEXT,
    profile_url TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Games table (if you want to store game information)
CREATE TABLE IF NOT EXISTS games (
    app_id VARCHAR(20) PRIMARY KEY,
    title VARCHAR(255) NOT NULL,
    image_url TEXT,
    price VARCHAR(50),
    release_date DATE,
    last_fetched TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Game tags table (if you want to store game tags)
CREATE TABLE IF NOT EXISTS game_tags (
    game_id VARCHAR(20) REFERENCES games(app_id) ON DELETE CASCADE,
    tag_id INTEGER NOT NULL,
    PRIMARY KEY (game_id, tag_id)
);

-- User tags table (custom tags created by users)
CREATE TABLE IF NOT EXISTS user_tags (
    id SERIAL PRIMARY KEY,
    user_id INTEGER REFERENCES users(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Wishlist items table
CREATE TABLE IF NOT EXISTS wishlist_items (
    user_id INTEGER REFERENCES users(id) ON DELETE CASCADE,
    game_id VARCHAR(20) REFERENCES games(app_id) ON DELETE CASCADE,
    notes TEXT,
    added_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, game_id)
);

-- Indexes for better query performance
CREATE INDEX IF NOT EXISTS idx_users_steam_id ON users(steam_id);
CREATE INDEX IF NOT EXISTS idx_wishlist_user_id ON wishlist_items(user_id);
CREATE INDEX IF NOT EXISTS idx_game_tags_game_id ON game_tags(game_id);
CREATE INDEX IF NOT EXISTS idx_user_tags_user_id ON user_tags(user_id);

-- Function to automatically update the updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Trigger to automatically update updated_at when users table is modified
CREATE TRIGGER update_users_updated_at 
    BEFORE UPDATE ON users 
    FOR EACH ROW 
    EXECUTE FUNCTION update_updated_at_column();
