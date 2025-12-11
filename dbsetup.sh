 #!/bin/bash
export PGPASSWORD=Password

# alter max connections
psql -h localhost -p 5432 -U platform_notifications_admin -d notificationsdb  \
-c "ALTER SYSTEM SET max_connections TO '200';"

# set up platform_notifications role
psql -h localhost -p 5432 -U platform_notifications_admin -d notificationsdb \
-c "DO \$\$
    BEGIN CREATE ROLE platform_notifications WITH LOGIN  PASSWORD 'Password';
    EXCEPTION WHEN duplicate_object THEN RAISE NOTICE '%, skipping', SQLERRM USING ERRCODE = SQLSTATE;
    END \$\$;"
