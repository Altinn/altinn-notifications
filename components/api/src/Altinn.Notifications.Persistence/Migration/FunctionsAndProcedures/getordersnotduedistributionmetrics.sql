create or replace function notifications.get_orders_notdue_distribution_metrics()
returns table
(
	day date,
	count bigint
)
language sql
as $$
	select o.requestedsendtime::date as day, count(*) from notifications.orders o where o.processedstatus = 'Registered' group by o.requestedsendtime::date order by day;
$$;
