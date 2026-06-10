-- Create the application role used by migration grant scripts.
-- The admin role (platform_notifications_admin) is created by POSTGRES_USER,
-- but migrations also grant permissions to this read/write application role.
CREATE ROLE platform_notifications WITH LOGIN PASSWORD 'Password';
