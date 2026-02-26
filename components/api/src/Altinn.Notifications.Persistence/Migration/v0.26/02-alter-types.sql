ALTER TYPE public.emailnotificationresulttype ADD VALUE IF NOT EXISTS 'Failed_RecipientReserved';

ALTER TYPE public.smsnotificationresulttype ADD VALUE IF NOT EXISTS 'Failed_Recipientreserved';
