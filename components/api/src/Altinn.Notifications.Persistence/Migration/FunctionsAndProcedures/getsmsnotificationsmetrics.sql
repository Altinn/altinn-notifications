create or replace function notifications.get_sms_notifications_metrics()
returns table
(
	anytimecount bigint,
	daytimecount bigint
)
language sql
as $$
	select
        (SELECT count(*) FROM notifications.smsnotifications sms WHERE sms.result = 'New'::smsnotificationresulttype and sendingtimepolicy = 1),
        (SELECT count(*) FROM notifications.smsnotifications sms WHERE sms.result = 'New'::smsnotificationresulttype and sendingtimepolicy = 2);
$$;
