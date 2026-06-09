ALTER TYPE public.emailnotificationresulttype ADD VALUE IF NOT EXISTS 'Delivered';
ALTER TYPE public.emailnotificationresulttype ADD VALUE IF NOT EXISTS 'Failed_InvalidEmailFormat';
ALTER TYPE public.emailnotificationresulttype ADD VALUE IF NOT EXISTS 'Failed_SupressedRecipient';
ALTER TYPE public.emailnotificationresulttype ADD VALUE IF NOT EXISTS 'Failed_TransientError';
ALTER TYPE public.emailnotificationresulttype ADD VALUE IF NOT EXISTS 'Failed_Bounced';
ALTER TYPE public.emailnotificationresulttype ADD VALUE IF NOT EXISTS 'Failed_FilteredSpam';
ALTER TYPE public.emailnotificationresulttype ADD VALUE IF NOT EXISTS 'Failed_Quarantined';



ALTER TYPE public.smsnotificationresulttype ADD VALUE IF NOT EXISTS 'Failed_InvalidRecipient';
ALTER TYPE public.smsnotificationresulttype ADD VALUE IF NOT EXISTS 'Failed_RecipientReserved';
ALTER TYPE public.smsnotificationresulttype ADD VALUE IF NOT EXISTS 'Delivered';
ALTER TYPE public.smsnotificationresulttype ADD VALUE IF NOT EXISTS 'Failed_BarredReceiver';
ALTER TYPE public.smsnotificationresulttype ADD VALUE IF NOT EXISTS 'Failed_Deleted';
ALTER TYPE public.smsnotificationresulttype ADD VALUE IF NOT EXISTS 'Failed_Expired';
ALTER TYPE public.smsnotificationresulttype ADD VALUE IF NOT EXISTS 'Failed_Undelivered';
ALTER TYPE public.smsnotificationresulttype ADD VALUE IF NOT EXISTS 'Failed_Rejected';