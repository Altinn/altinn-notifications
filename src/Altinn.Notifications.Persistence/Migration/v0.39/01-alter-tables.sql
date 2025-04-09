
DO $$
BEGIN
  IF EXISTS (SELECT 1
             FROM information_schema.columns
             WHERE table_schema = 'notifications'
               AND table_name   = 'orders'
               AND column_name  = 'sendingtimepolicy') THEN
    RAISE NOTICE 'Column "sendingtimepolicy" already exists in table "orders".';
  ELSE
    ALTER TABLE IF EXISTS notifications.orders
    ADD COLUMN sendingtimepolicy integer;
  END IF;
END $$;
