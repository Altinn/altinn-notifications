CREATE INDEX IF NOT EXISTS notifications_emailnotifications_retrying_idx
    ON notifications.emailnotifications
    USING btree (resulttime, _id)
    WHERE (result = 'Retrying'::emailnotificationresulttype);

CREATE INDEX IF NOT EXISTS notifications_smsnotifications_retrying_idx
    ON notifications.smsnotifications
    USING btree (resulttime, _id)
    WHERE (result = 'Retrying'::smsnotificationresulttype);
