DO $$  
BEGIN  
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'orderprocessingstate') THEN  
        CREATE TYPE orderprocessingstate AS ENUM ('Registered', 'Processing', 'Completed', 'Cancelled');  
    ELSE  
        BEGIN  
            IF NOT EXISTS (SELECT 1 FROM pg_enum WHERE enumtypid = 'orderprocessingstate'::regtype AND enumlabel = 'Cancelled') THEN  
                ALTER TYPE orderprocessingstate ADD VALUE 'Cancelled';  
            END IF;  
        END;  
    END IF;  
END $$;
