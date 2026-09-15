

CREATE INDEX IF NOT EXISTS notifications_retrying_processed_idx ON notifications.orders
	USING btree (processed, _id)
	INCLUDE (notificationorder)
	WHERE (processedstatus = 'Retrying'::orderprocessingstate);
