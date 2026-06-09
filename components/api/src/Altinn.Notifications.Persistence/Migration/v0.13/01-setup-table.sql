CREATE TABLE IF NOT EXISTS notifications.resourcelimitlog(
    id SERIAL PRIMARY KEY,
    emaillimittimeout TIMESTAMP WITH TIME ZONE
);

INSERT INTO notifications.resourcelimitlog (emaillimittimeout) values (NULL);
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE notifications.resourcelimitlog TO platform_notifications;
