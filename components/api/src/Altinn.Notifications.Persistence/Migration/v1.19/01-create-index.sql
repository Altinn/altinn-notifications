CREATE INDEX if not exists notifications_emailnotifications_sending_filtered ON notifications.emailnotifications USING btree (resulttime, _id) WHERE (result = 'Sending'::emailnotificationresulttype);
