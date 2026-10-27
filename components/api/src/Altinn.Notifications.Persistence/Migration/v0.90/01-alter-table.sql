ALTER TABLE notifications.emailnotifications
    ADD COLUMN IF NOT EXISTS total_attachment_size_bytes BIGINT NOT NULL DEFAULT 0;
