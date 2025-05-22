DO $$  
BEGIN  
    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON t.typnamespace = n.oid WHERE t.typname = 'orderprocessingstate' AND n.nspname = 'public') THEN  
        CREATE TYPE public.orderprocessingstate AS ENUM ('Registered', 'Processing', 'Completed', 'SendConditionNotMet', 'Cancelled', 'Processed');  
    ELSE  
       IF NOT EXISTS (SELECT 1 FROM pg_enum e JOIN pg_type t ON e.enumtypid = t.oid JOIN pg_namespace n ON t.typnamespace = n.oid 
                     WHERE t.typname = 'orderprocessingstate' AND n.nspname = 'public' AND e.enumlabel = 'Processed') THEN  
           ALTER TYPE public.orderprocessingstate ADD VALUE IF NOT EXISTS 'Processed';
       END IF;  
    END IF;  
END $$;