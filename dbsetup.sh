 #!/bin/bash
export PGPASSWORD=Password

psql -h localhost -p 5432 -U platform_notifications_admin -d notificationsdb -c "CREATE ROLE platform_notifications WITH   LOGIN  PASSWORD 'Password';"
