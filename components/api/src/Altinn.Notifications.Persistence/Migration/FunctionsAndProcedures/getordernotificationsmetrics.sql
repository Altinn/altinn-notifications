create or replace function notifications.get_order_notifications_metrics()
returns table
(
	ordersnotduecount bigint,
	orderspastduecount bigint
)
language sql
as $$
	-- TODO: Populate with real order backlog query.
	select
        (SELECT count(*) FROM notifications.orders WHERE processedstatus = 'Registered'::orderprocessingstate AND requestedsendtime > now() + INTERVAL '1 minute'),
        (SELECT count(*) FROM notifications.orders WHERE processedstatus = 'Registered'::orderprocessingstate AND requestedsendtime <= now() + INTERVAL '1 minute');
$$;
