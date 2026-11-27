create or replace function notifications.get_sms_notifications_metrics_v2()
returns table
(
	anytimecount bigint,
	daytimecount bigint,
	sendingcount bigint
)
language sql
as $$
	select
		(SELECT count(*) FROM notifications.smsnotifications sms WHERE sms.result = 'New'::smsnotificationresulttype and sendingtimepolicy = 1),
		(SELECT count(*) FROM notifications.smsnotifications sms WHERE sms.result = 'New'::smsnotificationresulttype and sendingtimepolicy = 2),
		(SELECT count(*) FROM notifications.smsnotifications sms WHERE sms.result = 'Sending'::smsnotificationresulttype);
$$;
