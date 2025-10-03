-- Add unique index on gatewayreference column for smsnotifications table
-- This enforces uniqueness at the database level while allowing multiple NULL values

CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ix_smsnotifications_gatewayreference
ON notifications.smsnotifications(gatewayreference)
WHERE gatewayreference IS NOT NULL;