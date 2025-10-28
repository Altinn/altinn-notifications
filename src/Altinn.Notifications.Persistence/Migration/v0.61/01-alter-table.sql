-- =====================================================================
-- Migration: Create partial index to speed up claiming of 'New' email rows
-- Purpose : Supports fast ORDER BY _id LIMIT n for result='New' batches
-- =====================================================================
CREATE INDEX IF NOT EXISTS notifications_emailnotifications_result_new
  ON notifications.emailnotifications (_id)
  INCLUDE (_orderid)
  WHERE result = 'New';

  -- Drop older, less specific index if it exists
  DROP INDEX IF EXISTS notifications.notifications_emailnotifications_result;
