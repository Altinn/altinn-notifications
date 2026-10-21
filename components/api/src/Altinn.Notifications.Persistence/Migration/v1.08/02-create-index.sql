CREATE INDEX IF NOT EXISTS notifications_emailnotifications_expirytime_filtered_notcomposed_anytime
	ON notifications.emailnotifications (expirytime)
	WHERE (result = 'New'::emailnotificationresulttype and iscomposed = false and sendingtimepolicy = 1);

CREATE INDEX IF NOT EXISTS notifications_emailnotifications_expirytime_filtered_notcomposed_daytime
	ON notifications.emailnotifications (expirytime)
	WHERE (result = 'New'::emailnotificationresulttype and iscomposed = false and sendingtimepolicy = 2);

DROP INDEX IF EXISTS notifications.notifications_emailnotifications_expirytime_filtered_notcomposed;
