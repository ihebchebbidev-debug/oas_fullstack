-- Sales Module Database Migration
-- Creates tables for sales, sale items, and sale activities

-- Create sales table
CREATE TABLE IF NOT EXISTS sales (
    id VARCHAR(50) PRIMARY KEY,
    title VARCHAR(255) NOT NULL,
    description TEXT,
    
    -- Contact Information
    contact_id INTEGER NOT NULL,
    
    -- Financial Information
    amount DECIMAL(15,2) NOT NULL DEFAULT 0,
    currency VARCHAR(3) NOT NULL DEFAULT 'TND',
    taxes DECIMAL(15,2) DEFAULT 0,
    discount DECIMAL(15,2) DEFAULT 0,
    total_amount DECIMAL(15,2) GENERATED ALWAYS AS (amount + COALESCE(taxes, 0) - COALESCE(discount, 0)) STORED,
    
    -- Status & Classification
    status VARCHAR(20) NOT NULL DEFAULT 'new_offer',
    stage VARCHAR(20) NOT NULL DEFAULT 'offer',
    priority VARCHAR(20),
    
    -- Billing Address
    billing_address TEXT,
    billing_postal_code VARCHAR(20),
    billing_country VARCHAR(100),
    
    -- Delivery Address
    delivery_address TEXT,
    delivery_postal_code VARCHAR(20),
    delivery_country VARCHAR(100),
    
    -- Dates
    estimated_close_date TIMESTAMP,
    actual_close_date TIMESTAMP,
    
    -- Assignment
    assigned_to VARCHAR(50),
    assigned_to_name VARCHAR(255),
    
    -- Tags
    tags TEXT[],
    
    -- Timestamps
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by VARCHAR(50) NOT NULL,
    last_activity TIMESTAMP,
    
    -- Offer Reference
    offer_id VARCHAR(50),
    converted_from_offer_at TIMESTAMP,
    
    -- Loss tracking
    lost_reason VARCHAR(500),
    
    -- Fulfillment tracking
    materials_fulfillment VARCHAR(20),
    service_orders_status VARCHAR(20),
    
    -- Foreign Keys
    CONSTRAINT fk_sales_contact FOREIGN KEY (contact_id) REFERENCES contacts(id) ON DELETE RESTRICT,
    CONSTRAINT fk_sales_offer FOREIGN KEY (offer_id) REFERENCES offers(id) ON DELETE SET NULL
);

-- Create sale_items table
CREATE TABLE IF NOT EXISTS sale_items (
    id VARCHAR(50) PRIMARY KEY,
    sale_id VARCHAR(50) NOT NULL,
    
    -- Item Information
    type VARCHAR(20) NOT NULL,
    article_id VARCHAR(50) NOT NULL,
    item_name VARCHAR(255) NOT NULL,
    item_code VARCHAR(100),
    description TEXT,
    
    -- Quantity & Pricing
    quantity DECIMAL(10,2) NOT NULL DEFAULT 1,
    unit_price DECIMAL(15,2) NOT NULL DEFAULT 0,
    
    -- Discount
    discount DECIMAL(15,2) DEFAULT 0,
    discount_type VARCHAR(20) DEFAULT 'percentage',
    
    -- Calculated total price
    total_price DECIMAL(15,2) GENERATED ALWAYS AS (
        quantity * unit_price - COALESCE(
            CASE 
                WHEN discount_type = 'percentage' THEN (quantity * unit_price * discount / 100)
                WHEN discount_type = 'fixed' THEN discount
                ELSE 0
            END, 0
        )
    ) STORED,
    
    -- Installation reference
    installation_id VARCHAR(50),
    installation_name VARCHAR(255),
    
    -- Service Order tracking
    requires_service_order BOOLEAN DEFAULT FALSE,
    service_order_generated BOOLEAN DEFAULT FALSE,
    service_order_id VARCHAR(50),
    
    -- Fulfillment status
    fulfillment_status VARCHAR(20),
    
    -- Foreign Keys
    CONSTRAINT fk_sale_items_sale FOREIGN KEY (sale_id) REFERENCES sales(id) ON DELETE CASCADE
);

-- Create sale_activities table
CREATE TABLE IF NOT EXISTS sale_activities (
    id VARCHAR(50) PRIMARY KEY,
    sale_id VARCHAR(50) NOT NULL,
    
    -- Activity Information
    type VARCHAR(50) NOT NULL,
    description TEXT NOT NULL,
    details TEXT,
    
    -- Old/New Values for tracking
    old_value VARCHAR(255),
    new_value VARCHAR(255),
    
    -- Timestamps & User
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by VARCHAR(50) NOT NULL,
    created_by_name VARCHAR(255) NOT NULL,
    
    -- Foreign Keys
    CONSTRAINT fk_sale_activities_sale FOREIGN KEY (sale_id) REFERENCES sales(id) ON DELETE CASCADE
);

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS idx_sales_contact_id ON sales(contact_id);
CREATE INDEX IF NOT EXISTS idx_sales_status ON sales(status);
CREATE INDEX IF NOT EXISTS idx_sales_stage ON sales(stage);
CREATE INDEX IF NOT EXISTS idx_sales_priority ON sales(priority);
CREATE INDEX IF NOT EXISTS idx_sales_created_at ON sales(created_at);
CREATE INDEX IF NOT EXISTS idx_sales_updated_at ON sales(updated_at);
CREATE INDEX IF NOT EXISTS idx_sales_assigned_to ON sales(assigned_to);
CREATE INDEX IF NOT EXISTS idx_sales_offer_id ON sales(offer_id);

CREATE INDEX IF NOT EXISTS idx_sale_items_sale_id ON sale_items(sale_id);
CREATE INDEX IF NOT EXISTS idx_sale_items_type ON sale_items(type);
CREATE INDEX IF NOT EXISTS idx_sale_items_article_id ON sale_items(article_id);

CREATE INDEX IF NOT EXISTS idx_sale_activities_sale_id ON sale_activities(sale_id);
CREATE INDEX IF NOT EXISTS idx_sale_activities_type ON sale_activities(type);
CREATE INDEX IF NOT EXISTS idx_sale_activities_created_at ON sale_activities(created_at);

-- Create trigger to auto-update updated_at timestamp
CREATE OR REPLACE FUNCTION update_sales_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_update_sales_updated_at
    BEFORE UPDATE ON sales
    FOR EACH ROW
    EXECUTE FUNCTION update_sales_updated_at();

-- Create trigger to log status changes
CREATE OR REPLACE FUNCTION log_sale_status_change()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD.status IS DISTINCT FROM NEW.status THEN
        INSERT INTO sale_activities (
            id,
            sale_id,
            type,
            description,
            old_value,
            new_value,
            created_at,
            created_by,
            created_by_name
        ) VALUES (
            'ACT-' || EXTRACT(EPOCH FROM NOW())::BIGINT || '-' || SUBSTRING(MD5(RANDOM()::TEXT) FROM 1 FOR 8),
            NEW.id,
            'status_change',
            'Sale status changed from ' || COALESCE(OLD.status, 'none') || ' to ' || NEW.status,
            OLD.status,
            NEW.status,
            NOW(),
            NEW.created_by,
            COALESCE(NEW.assigned_to_name, 'System')
        );
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_log_sale_status_change
    AFTER UPDATE ON sales
    FOR EACH ROW
    EXECUTE FUNCTION log_sale_status_change();

-- Create trigger to log sale creation
CREATE OR REPLACE FUNCTION log_sale_creation()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO sale_activities (
        id,
        sale_id,
        type,
        description,
        created_at,
        created_by,
        created_by_name
    ) VALUES (
        'ACT-' || EXTRACT(EPOCH FROM NOW())::BIGINT || '-' || SUBSTRING(MD5(RANDOM()::TEXT) FROM 1 FOR 8),
        NEW.id,
        'created',
        'Sale created: ' || NEW.title,
        NOW(),
        NEW.created_by,
        COALESCE(NEW.assigned_to_name, 'System')
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_log_sale_creation
    AFTER INSERT ON sales
    FOR EACH ROW
    EXECUTE FUNCTION log_sale_creation();

-- Create trigger to recalculate sale amount when items change
CREATE OR REPLACE FUNCTION recalculate_sale_amount()
RETURNS TRIGGER AS $$
BEGIN
    UPDATE sales
    SET amount = (
        SELECT COALESCE(SUM(total_price), 0)
        FROM sale_items
        WHERE sale_id = COALESCE(NEW.sale_id, OLD.sale_id)
    )
    WHERE id = COALESCE(NEW.sale_id, OLD.sale_id);
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_recalculate_sale_amount_insert
    AFTER INSERT ON sale_items
    FOR EACH ROW
    EXECUTE FUNCTION recalculate_sale_amount();

CREATE TRIGGER trigger_recalculate_sale_amount_update
    AFTER UPDATE ON sale_items
    FOR EACH ROW
    EXECUTE FUNCTION recalculate_sale_amount();

CREATE TRIGGER trigger_recalculate_sale_amount_delete
    AFTER DELETE ON sale_items
    FOR EACH ROW
    EXECUTE FUNCTION recalculate_sale_amount();
