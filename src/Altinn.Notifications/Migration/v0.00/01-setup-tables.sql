-- SCHEMA: notifications

CREATE SCHEMA IF NOT EXISTS notifications
AUTHORIZATION platform_notifications_admin;

-- Table: notifications.notifications

CREATE TABLE IF NOT EXISTS notifications.notifications
(
    id BIGSERIAL,
    sendtime timestamptz NOT NULL,
    instanceid character varying COLLATE pg_catalog."default",
    partyreference character varying COLLATE pg_catalog."default",
    sender character varying COLLATE pg_catalog."default" NOT NULL,
    CONSTRAINT notifications_pkey PRIMARY KEY (id)
)
TABLESPACE pg_default;


-- Table: notifications.messages

CREATE TABLE IF NOT EXISTS notifications.messages
(
    id BIGSERIAL,
    notificationid bigint NOT NULL,
    emailsubject character varying COLLATE pg_catalog."default",
    emailbody character varying COLLATE pg_catalog."default",
    smstext character varying COLLATE pg_catalog."default",
    "language" character varying COLLATE pg_catalog."default",
    CONSTRAINT messages_pkey PRIMARY KEY (id)
)
TABLESPACE pg_default;


-- Table: notifications.targets

CREATE TABLE IF NOT EXISTS notifications.targets
(
    id BIGSERIAL,
    notificationid bigint NOT NULL,
    channeltype character varying COLLATE pg_catalog."default" NOT NULL,
    "address" character varying COLLATE pg_catalog."default",
    "sent" timestamptz,
    CONSTRAINT targets_pkey PRIMARY KEY (id)
)
TABLESPACE pg_default;
