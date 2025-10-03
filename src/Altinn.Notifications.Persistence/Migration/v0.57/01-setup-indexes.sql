-- Add indexes to improve UpdateSendStatus performance and eliminate OR condition timeouts
-- Issue #1050: Intermittent 30s+ timeout in EmailNotificationRepository.UpdateSendStatus

-- Index for operationid lookups - critical for email status updates
CREATE INDEX IF NOT EXISTS idx_emailnotifications_operationid ON notifications.emailnotifications (operationid)
WHERE operationid IS NOT NULL;
