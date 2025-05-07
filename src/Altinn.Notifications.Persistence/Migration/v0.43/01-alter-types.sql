/*
 * Script to resolve duplicate enum values in smsnotificationresulttype
 *
 * Problem: The enum has both 'Failed_RecipientReserved' and 'Failed_Recipientreserved' values
 * Solution: Normalize to proper case version and recreate enum without duplicate value
 * More details: Refer to issue https://github.com/Altinn/altinn-notifications/issues/810
 */

-- 1. Create a new enum type
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'smsnotificationresulttype_new' AND typnamespace = 'public'::regnamespace) THEN
    CREATE TYPE public.smsnotificationresulttype_new AS ENUM (
      'New',
      'Sending',
      'Accepted',
      'Failed_InvalidReceiver',
      'Failed',
      'Failed_RecipientNotIdentified',
      'Delivered',
      'Failed_BarredReceiver',
      'Failed_Deleted',
      'Failed_Expired',
      'Failed_Undelivered',
      'Failed_Rejected',
      'Failed_InvalidRecipient',
      'Failed_RecipientReserved'
      );
  END IF;
END
$$;

-- 2. Migrate table column and normalizing existing data
ALTER TABLE notifications.smsnotifications
  ALTER COLUMN result TYPE public.smsnotificationresulttype_new
  USING (CASE 
         WHEN result::text = 'Failed_Recipientreserved' THEN 'Failed_RecipientReserved'
         ELSE result::text
         END)::public.smsnotificationresulttype_new;

-- 3. Find and update any dependencies before dropping the old enum
DO $$
DECLARE
  rec RECORD;
  def TEXT;
BEGIN
  FOR rec IN
    SELECT n.nspname, c.relname, c.relkind
    FROM pg_depend d
    JOIN pg_type t ON d.objid = t.oid
    JOIN pg_class c ON d.refobjid = c.oid
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE t.typname = 'smsnotificationresulttype' AND c.relkind IN ('v','m')
  LOOP
    IF rec.relkind = 'v' THEN
      def := pg_get_viewdef(format('%I.%I', rec.nspname, rec.relname), true);
      EXECUTE format('CREATE OR REPLACE VIEW %I.%I AS %s', rec.nspname, rec.relname, def);
    ELSE
      def := pg_get_viewdef(format('%I.%I', rec.nspname, rec.relname), true);
      EXECUTE format('CREATE OR REPLACE MATERIALIZED VIEW %I.%I AS %s', rec.nspname, rec.relname, def);
      EXECUTE format('REFRESH MATERIALIZED VIEW %I.%I', rec.nspname, rec.relname);
    END IF;
  END LOOP;

  FOR rec IN
    SELECT n.nspname, p.proname, p.oid
    FROM pg_depend d
    JOIN pg_type t ON d.objid = t.oid
    JOIN pg_proc p ON d.refobjid = p.oid
    JOIN pg_namespace n ON n.oid = p.pronamespace
    WHERE t.typname = 'smsnotificationresulttype'
  LOOP
    def := pg_get_functiondef(rec.oid);
    def := regexp_replace(def, '\m smsnotificationresulttype \M','smsnotificationresulttype_new', 'g');
    EXECUTE def;
  END LOOP;
END
$$;

-- 4. Drop the old enum type with CASCADE option to force removal of any remaining dependencies
DROP TYPE IF EXISTS public.smsnotificationresulttype CASCADE;

-- 5. Rename new enum type to standard name
ALTER TYPE public.smsnotificationresulttype_new RENAME TO smsnotificationresulttype;

-- 6. Set correct ownership permissions
ALTER TYPE public.smsnotificationresulttype OWNER TO platform_notifications_admin;