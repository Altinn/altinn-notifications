create database notifications;
create SCHEMA notifications;
CREATE user platform_notifications_admin with encrypted password 'notsosecret';
GRANT all privileges on database notifications to platform_notifications_admin;

CREATE user platform_notifications with encrypted password 'notsosecreteither';
GRANT connect on database notifications to platform_notifications;
GRANT  USAGE  ON SCHEMA notifications TO platform_notifications;
GRANT SELECT,INSERT,UPDATE,REFERENCES,DELETE,TRUNCATE,REFERENCES,TRIGGER ON ALL TABLES IN SCHEMA notifications TO platform_notifications;
GRANT ALL ON ALL SEQUENCES IN SCHEMA notifications TO platform_notifications;