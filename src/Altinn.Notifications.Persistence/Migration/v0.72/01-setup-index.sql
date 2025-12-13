-- To support efficient querying in getorders_pastsendtime function
CREATE INDEX IF NOT EXISTS notifications_registered_reqtime_idx
ON notifications.orders (requestedsendtime, _id)
WHERE processedstatus = 'Registered';
