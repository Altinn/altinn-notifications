CREATE TABLE IF NOT EXISTS notifications.senderaddresstype (
    id SMALLINT NOT NULL,
    name text NOT NULL,
    CONSTRAINT senderaddresstype_pkey PRIMARY KEY (id),
    CONSTRAINT senderaddresstype_name_key UNIQUE (name)
);

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE notifications.senderaddresstype TO platform_notifications;

INSERT INTO notifications.senderaddresstype (id, name)
VALUES
    (1, 'SMS'),
    (2, 'Email')
ON CONFLICT (id) DO NOTHING;

CREATE TABLE IF NOT EXISTS notifications.serviceowner (
    id BIGINT GENERATED ALWAYS AS IDENTITY,
    serviceowner text NOT NULL,
    addresstypeid SMALLINT NOT NULL,
    senderaddress text NOT NULL,
    CONSTRAINT serviceowner_pkey PRIMARY KEY (id),
    CONSTRAINT serviceowner_addresstypeid_fkey FOREIGN KEY (addresstypeid) REFERENCES notifications.senderaddresstype (id),
    CONSTRAINT serviceowner_addresstypeid_senderaddress_key UNIQUE (addresstypeid, senderaddress)
);

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE notifications.serviceowner TO platform_notifications;
