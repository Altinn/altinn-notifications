-- Add reason and message columns to deaddeliveryreports table
ALTER TABLE notifications.deaddeliveryreports
ADD COLUMN IF NOT EXISTS reason TEXT,
ADD COLUMN IF NOT EXISTS message TEXT;
