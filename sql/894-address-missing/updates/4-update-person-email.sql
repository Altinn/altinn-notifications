-- fnumber_ak: 31884298868

-- Set my email for testing
UPDATE contact_and_reservation.person
SET email_address = 'martin.vagseter.jakobsen@digdir.no'
WHERE fnumber_ak = '31884298868';

-- Remove email while sending a notification
UPDATE contact_and_reservation.person
SET email_address = NULL
WHERE fnumber_ak = '31884298868';

-- Restore email after testing
UPDATE contact_and_reservation.person
SET email_address = 'nullstillt@default.digdir.no'
WHERE fnumber_ak = '31884298868';

-- Check the current state
SELECT email_address,
       mobile_phone_number,
       fnumber_ak
FROM contact_and_reservation.person
WHERE fnumber_ak = '31884298868';
