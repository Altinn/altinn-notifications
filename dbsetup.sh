 #!/bin/bash
export PGPASSWORD=Password

# set up platform_notifications role
psql -h localhost -p 5432 -U platform_notifications_admin -d notificationsdb \
-c "DO \$\$
    BEGIN CREATE ROLE platform_notifications WITH LOGIN  PASSWORD 'Password';
    EXCEPTION WHEN duplicate_object THEN RAISE NOTICE '%, skipping', SQLERRM USING ERRCODE = SQLSTATE;
    END \$\$;"

#psql -h localhost -p 5432 -U platform_notifications_admin -d notificationsdb \
#-c "INSERT INTO notifications.orders(
#	alternateid, creatorname, sendersreference, created, sendtime, notificationorder)
#	VALUES ('c33ecdb6-cf41-4178-b1e8-006dd57f5298', 'ttd', 'senders-reference', '2023-07-14 11:58:26.477101+02', '2023-07-14 11:58:23.865236+02', '{\"id\": \"c33ecdb6-cf41-4178-b1e8-006dd57f5297\", \"created\": \"2023-07-14T09:58:26.4771012Z\", \"creator\": {\"shortName\": \"ttd\"}, \"sendTime\": \"2023-07-14T09:58:23.8652361Z\", \"templates\": [{\"$\": \"email\", \"body\": \"email-body\", \"type\": \"Email\", \"subject\": \"email-subject\", \"contentType\": \"Html\", \"fromAddress\": \"sender@domain.com\"}], \"recipients\": [{\"addressInfo\": [{\"$\": \"email\", \"addressType\": \"Email\", \"emailAddress\": \"recipient1@domain.com\"}], \"recipientId\": \"\"}, {\"addressInfo\": [{\"$\": \"email\", \"addressType\": \"Email\", \"emailAddress\": \"recipient2@domain.com\"}], \"recipientId\": \"\"}], \"sendersReference\": \"senders-reference\", \"notificationChannel\": \"Email\"}');"