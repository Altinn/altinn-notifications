-- =====================================================================
-- Migration: Create partial index to speed up claiming of 'New' SMS rows
-- Purpose : Supports fast ORDER BY _id LIMIT n for result='New' batches
-- =====================================================================
CREATE INDEX CONCURRENTLY IF NOT EXISTS notifications_smsnotifications_result_new
  ON notifications.smsnotifications (_id)
  INCLUDE (_orderid)
  WHERE result = 'New';