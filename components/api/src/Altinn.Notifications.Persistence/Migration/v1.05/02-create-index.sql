CREATE INDEX IF NOT EXISTS notifications_emailnotifications_expirytime_filtered_composed
	ON notifications.emailnotifications (expirytime)
	WHERE (result = 'New'::emailnotificationresulttype and iscomposed = true);

CREATE INDEX IF NOT EXISTS notifications_emailnotifications_expirytime_filtered_notcomposed
	ON notifications.emailnotifications (expirytime)
	WHERE (result = 'New'::emailnotificationresulttype and iscomposed = false);

DROP INDEX IF EXISTS notifications.notifications_emailnotifications_result_new_v2;
DROP INDEX IF EXISTS notifications.notifications_emailnotifications_result_new;

CREATE INDEX IF NOT EXISTS notifications_smsnotifications_expirytime_filtered_anytime
	ON notifications.smsnotifications (expirytime)
	WHERE (result = 'New'::smsnotificationresulttype and sendingtimepolicy = 1);

CREATE INDEX IF NOT EXISTS notifications_smsnotifications_expirytime_filtered_daytime
	ON notifications.smsnotifications (expirytime)
	WHERE (result = 'New'::smsnotificationresulttype and sendingtimepolicy = 2);

DROP INDEX IF EXISTS notifications.notifications_smsnotifications_result_new;
