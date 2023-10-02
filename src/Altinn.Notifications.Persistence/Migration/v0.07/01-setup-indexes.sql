CREATE INDEX IF NOT EXISTS notifications_processedstatus_requestedsendtime ON notifications.orders (processedstatus, requestedsendtime) INCLUDE (_id);
