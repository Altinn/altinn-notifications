-- Schedule refresh jobs (idempotent: unschedule existing job with the same name first)
SELECT cron.unschedule(jobid) FROM cron.job WHERE jobname = 'refresh_email_metrics_recent';
SELECT cron.schedule_in_database(
    'refresh_email_metrics_recent',
    '5 0 * * *',
    $$REFRESH MATERIALIZED VIEW CONCURRENTLY notifications.email_metrics_recent$$,
    'notifications'
);

SELECT cron.unschedule(jobid) FROM cron.job WHERE jobname = 'refresh_sms_metrics_recent';
SELECT cron.schedule_in_database(
    'refresh_sms_metrics_recent',
    '10 0 * * *',
    $$REFRESH MATERIALIZED VIEW CONCURRENTLY notifications.sms_metrics_recent$$,
    'notifications'
);