create or replace function notifications.get_email_notifications_metrics_v2()
returns table
(
    anytimecount bigint,
    daytimecount bigint,
    composedcount bigint,
    sendingcount bigint
)
language sql
as $$
    SELECT
        (SELECT count(*) FROM notifications.emailnotifications email WHERE email.result = 'New'::emailnotificationresulttype and sendingtimepolicy = 1 and iscomposed = false),
        (SELECT count(*) FROM notifications.emailnotifications email WHERE email.result = 'New'::emailnotificationresulttype and sendingtimepolicy = 2 and iscomposed = false),
        (SELECT count(*) FROM notifications.emailnotifications email WHERE email.result = 'New'::emailnotificationresulttype and iscomposed = true),
        (SELECT count(*) FROM notifications.emailnotifications email WHERE email.result = 'Sending'::emailnotificationresulttype);
$$;