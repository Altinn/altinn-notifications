ALTER TABLE notifications.emailnotifications
    ADD COLUMN IF NOT EXISTS encoded_attachments_size BIGINT NOT NULL DEFAULT 0;
