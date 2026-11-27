create index if not exists
    notifications_emailnotifications_result_resulttime
    on notifications.emailnotifications (result, resulttime);

create index if not exists
    notifications_smsnotifications_result_resulttime
    on notifications.smsnotifications (result, resulttime);