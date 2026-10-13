CREATE TABLE IF NOT EXISTS notifications.sendingtimepolicy (
	id SMALLINT NOT NULL,
	description text NOT NULL,
	CONSTRAINT sendingtimepolicy_pkey PRIMARY KEY (id)
);
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE notifications.sendingtimepolicy TO platform_notifications;
INSERT INTO notifications.sendingtimepolicy (id, description) VALUES (1, 'Anytime') ON CONFLICT DO NOTHING;
INSERT INTO notifications.sendingtimepolicy (id, description) VALUES (2, 'Daytime') ON CONFLICT DO NOTHING;

ALTER TABLE notifications.orders  SET (autovacuum_vacuum_scale_factor = 0, autovacuum_vacuum_threshold = 1000, autovacuum_vacuum_cost_delay = 0);
ALTER TABLE notifications.emailnotifications   SET (autovacuum_vacuum_scale_factor = 0, autovacuum_vacuum_threshold = 1000, autovacuum_vacuum_cost_delay = 0);
ALTER TABLE notifications.smsnotifications   SET (autovacuum_vacuum_scale_factor = 0, autovacuum_vacuum_threshold = 1000, autovacuum_vacuum_cost_delay = 0);

ALTER TABLE notifications.emailnotifications ADD COLUMN IF NOT EXISTS sendingtimepolicy SMALLINT NULL DEFAULT 1;
ALTER TABLE notifications.smsnotifications ADD COLUMN IF NOT EXISTS sendingtimepolicy SMALLINT NULL DEFAULT 2;
ALTER TABLE notifications.emailnotifications ADD COLUMN IF NOT EXISTS iscomposed BOOLEAN NULL DEFAULT false;