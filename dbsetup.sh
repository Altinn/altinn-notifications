 #!/bin/bash
export PGPASSWORD=Password

# The timeout value (e.g., 10 seconds)
TIMEOUT='10s'

# Set the parameter for the entire system
psql -c "ALTER SYSTEM SET idle_session_timeout = '${TIMEOUT}';"

# set up platform_notifications role
psql -h localhost -p 5432 -U platform_notifications_admin -d notificationsdb \
-c "DO \$\$
    BEGIN CREATE ROLE platform_notifications WITH LOGIN  PASSWORD 'Password';
    EXCEPTION WHEN duplicate_object THEN RAISE NOTICE '%, skipping', SQLERRM USING ERRCODE = SQLSTATE;
    END \$\$;"