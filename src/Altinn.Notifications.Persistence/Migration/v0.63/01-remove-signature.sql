-- Drop old function signature before recreating with new return types
-- This is necessary because PostgreSQL cannot change return types with CREATE OR REPLACE

DROP FUNCTION IF EXISTS notifications.updateemailnotification(text, text, uuid);
