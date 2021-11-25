-- SCHEMA: notifications

CREATE SCHEMA IF NOT EXISTS notifications
AUTHORIZATION platform_notifications_admin;

-- Table: notifications.notifications

CREATE TABLE IF NOT EXISTS notifications.notifications
(
    id BIGSERIAL,
    --source character varying COLLATE pg_catalog."default" NOT NULL,
    --"time" timestamptz  NOT NULL,
    CONSTRAINT notifications_pkey PRIMARY KEY (id)
)

-- Table: notifications.messages

CREATE TABLE IF NOT EXISTS notifications.messages
(
    id BIGSERIAL,
    --source character varying COLLATE pg_catalog."default" NOT NULL,
    --"time" timestamptz  NOT NULL,
    CONSTRAINT messages_pkey PRIMARY KEY (id)
)

-- Table: notifications.targets

CREATE TABLE IF NOT EXISTS notifications.targets
(
    id BIGSERIAL,
    CONSTRAINT targets_pkey PRIMARY KEY (id)
)

TABLESPACE pg_default;

-- Procecure: insert_event

--CREATE OR REPLACE PROCEDURE events.insert_event(
--	id character varying,
--	source character varying,
--	subject character varying,
--	type character varying,
--	cloudevent text)
--LANGUAGE 'plpgsql'
--AS $BODY$
--DECLARE currentTime timestamptz; 
--DECLARE currentTimeString character varying; 
--BEGIN
--  SET TIME ZONE UTC;
--  currentTime := NOW();
--  currentTimeString :=  to_char(currentTime, 'YYYY-MM-DD"T"HH24:MI:SS.USOF');
--
--INSERT INTO events.events(id, source, subject, type, "time", cloudevent)
--	VALUES ($1, $2, $3, $4, currentTime,  substring($5 from 1 for length($5) -1)  || ',"time": "' || currentTimeString || '"}');
--	
--END;
--$BODY$;
