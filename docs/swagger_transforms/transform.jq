.openapi = "3.0.3"
| del(.security)
| del(.components.securitySchemes)
| .paths |= map_values(map_values(del(.security)))
| .paths |= with_entries(.key |= sub("/notifications/api/v1"; ""))
| .paths |= map_values(map_values(.tags |= map(if . == "FutureOrders" then "Orders" elif . == "Shipment" then "Orders" elif . == "InstantOrders" then "Instant Orders" else . end)))
| .servers |= map(select(.url | contains("localhost") | not))
| .servers |= map(.url |= sub("altinn.no/"; "altinn.no/notifications/api/v1"))
| del(.paths."/tests/sendcondition")
| del(.paths."/metrics", .paths."/metrics/sms", .paths."/metrics/email")
| del(.tags)
| .tags += [{"name": "Deprecated", "description": "Legacy endpoints. Still supported, but going forward please use '/future/*' endpoints instead."}]
| .tags += [{"name": "Order", "description": "Create notifications (queued processing)"}]
| .tags += [{"name": "Status", "description": "Get status/updates for notifications already ordered"}]
| .tags += [{"name": "Instant Orders", "description": "Create and send instant notifications to single recipients"}]
| .paths |= with_entries(
    if (.key | startswith("/orders")) or .key == "/future/orders/instant" then .value |= map_values(.tags = ["Deprecated"])
    elif .key == "/future/orders" then .value |= map_values(.tags = ["Order"])
    elif .key | startswith("/future/shipment") then .value |= map_values(.tags = ["Status"])
    else . end)
