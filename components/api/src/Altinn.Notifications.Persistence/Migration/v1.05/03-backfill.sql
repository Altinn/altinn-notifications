update notifications.emailnotifications e
	set
		iscomposed = CASE WHEN o.type = 'Composed'::notificationordertype THEN TRUE ELSE FALSE END,
		sendingtimepolicy = CASE WHEN o.sendingtimepolicy = 2 OR o.sendingtimepolicy IS NULL THEN 2 ELSE 1 END
	from notifications.orders o
	where e._orderid = o._id and e.expirytime >= now();

update notifications.smsnotifications s
	set
		sendingtimepolicy = CASE WHEN o.sendingtimepolicy = 2 OR o.sendingtimepolicy IS NULL THEN 2 ELSE 1 END
	from notifications.orders o
	where s._orderid = o._id and s.expirytime >= now();

ALTER TABLE notifications.emailnotifications
    ADD CONSTRAINT emailnotifications_sendingtimepolicy_fkey
    FOREIGN KEY (sendingtimepolicy)
    REFERENCES notifications.sendingtimepolicy (id)
    NOT VALID;

ALTER TABLE notifications.smsnotifications
    ADD CONSTRAINT smsnotifications_sendingtimepolicy_fkey
    FOREIGN KEY (sendingtimepolicy)
    REFERENCES notifications.sendingtimepolicy (id)
    NOT VALID;

ALTER TABLE notifications.emailnotifications
    VALIDATE CONSTRAINT emailnotifications_sendingtimepolicy_fkey;

ALTER TABLE notifications.smsnotifications
    VALIDATE CONSTRAINT smsnotifications_sendingtimepolicy_fkey;

