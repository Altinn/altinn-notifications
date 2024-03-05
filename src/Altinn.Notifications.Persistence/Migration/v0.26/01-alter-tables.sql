-- Modify table emailnotifications: Remove the recipientid column
ALTER TABLE notifications.emailnotifications
DROP COLUMN IF EXISTS recipientid;

-- Modify table smsnotifications: Add new columns recipientorgno and recipientnin
ALTER TABLE notifications.emailnotifications
ADD COLUMN IF NOT EXISTS recipientorgno text,
ADD COLUMN IF NOT EXISTS recipientnin text;


-- Modify table smsnotifications: Remove the recipientid column
ALTER TABLE notifications.smsnotifications
DROP COLUMN IF EXISTS recipientid;

-- Modify table smsnotifications: Add new columns recipientorgno and recipientnin
ALTER TABLE notifications.smsnotifications
ADD COLUMN IF NOT EXISTS recipientorgno text,
ADD COLUMN IF NOT EXISTS recipientnin text;