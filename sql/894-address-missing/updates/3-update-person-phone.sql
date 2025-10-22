-- fnumber_ak: 09854096734

-- Set my phone number for testing
UPDATE contact_and_reservation.person
SET mobile_phone_number = '+4798491711'
WHERE fnumber_ak = '09854096734';

-- Remove phone number while sending a notification
UPDATE contact_and_reservation.person
SET mobile_phone_number = NULL
WHERE fnumber_ak = '09854096734';

-- Restore phone number after testing
UPDATE contact_and_reservation.person
SET mobile_phone_number = '+4799999999'
WHERE fnumber_ak = '09854096734';

-- Check the current state
SELECT email_address,
       mobile_phone_number,
       fnumber_ak
FROM contact_and_reservation.person
WHERE fnumber_ak = '09854096734';
