
CREATE INDEX IF NOT EXISTS notifications_registered_reqtime_idx_v2 ON notifications.orders
	USING btree (requestedsendtime, _id)
	INCLUDE (notificationorder)
	WHERE (processedstatus = 'Registered'::orderprocessingstate);

CREATE INDEX IF NOT EXISTS notifications_retrying_processed_idx ON notifications.orders
	USING btree (processed, _id)
	INCLUDE (notificationorder)
	WHERE (processedstatus = 'Retrying'::orderprocessingstate);

DROP INDEX IF EXISTS notifications.notifications_registered_reqtime_idx;
