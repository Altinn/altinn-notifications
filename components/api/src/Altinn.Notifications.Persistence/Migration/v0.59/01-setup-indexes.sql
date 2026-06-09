-- Add unique index on gatewayreference column for smsnotifications table
-- Issue #1014: Add UNIQUE constraint on notifications.smsnotifications.gatewayreference column

CREATE UNIQUE INDEX IF NOT EXISTS notifications_smsnotifications_gatewayreference
ON notifications.smsnotifications(gatewayreference)
WHERE gatewayreference IS NOT NULL;