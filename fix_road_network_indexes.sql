-- Fix indexes for road_network table
-- MariaDB compatible version

-- Drop the problematic index if it exists
DROP INDEX IF EXISTS idx_road_network_name_lower ON road_network;

-- Create a regular index on the name column
-- MariaDB will use this for case-insensitive searches with LOWER()
CREATE INDEX IF NOT EXISTS idx_road_network_name ON road_network (name);

-- Ensure other indexes exist
CREATE INDEX IF NOT EXISTS idx_road_network_province_left ON road_network (province_uid_left);
CREATE INDEX IF NOT EXISTS idx_road_network_province_right ON road_network (province_uid_right);
CREATE INDEX IF NOT EXISTS idx_road_network_type ON road_network (type);

-- Add composite indexes for better search performance
CREATE INDEX IF NOT EXISTS idx_road_network_name_type ON road_network (name, type);
CREATE INDEX IF NOT EXISTS idx_road_network_provinces ON road_network (province_uid_left, province_uid_right);

-- Show the indexes to verify
SHOW INDEXES FROM road_network;