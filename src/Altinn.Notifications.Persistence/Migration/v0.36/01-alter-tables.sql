-- Modify the table emailnotifications: Add two new columns customizedbody and customizedsubject
ALTER TABLE notifications.emailnotifications
ADD COLUMN IF NOT EXISTS customizedbody text,
ADD COLUMN IF NOT EXISTS customizedsubject text;

-- Modify table smsnotifications: Add one new column customizedbody
ALTER TABLE notifications.smsnotifications
ADD COLUMN IF NOT EXISTS customizedbody text;
