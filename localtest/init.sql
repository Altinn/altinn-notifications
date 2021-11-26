CREATE DATABASE notificationsdb;
create SCHEMA notifications;
CREATE user platform_notifications_admin with encrypted password 'Password';
GRANT all privileges on database notificationsdb to platform_notifications_admin;

CREATE user platform_notifications with encrypted password 'Password';