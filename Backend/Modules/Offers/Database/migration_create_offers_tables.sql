-- =====================================================
-- Offers Module - Database Migration
-- Description: Creates all tables, indexes, triggers, and functions for the Offers module
-- Version: 1.0.0
-- Execute this in Neon SQL Editor
-- =====================================================

-- =====================================================
-- 1. OFFERS TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS offers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- Basic Information
    title VARCHAR(500) NOT NULL,
    description TEXT,
    notes TEXT,
    
    -- Contact Information
    contact_id INTEGER NOT NULL REFERENCES contacts(id) ON DELETE RESTRICT,
    
    -- Financial Information
    amount DECIMAL(18, 2) NOT NULL DEFAULT 0,
    currency VARCHAR(3) NOT NULL DEFAULT 'EUR',
    tax_rate DECIMAL(5, 2) DEFAULT 0,
    tax_amount DECIMAL(18, 2) DEFAULT 0,
    discount_amount DECIMAL(18, 2) DEFAULT 0,
    total_amount DECIMAL(18, 2) GENERATED ALWAYS AS (amount + COALESCE(tax_amount, 0) - COALESCE(discount_amount, 0)) STORED,
    
    -- Status and Classification
    status VARCHAR(50) NOT NULL DEFAULT 'draft' CHECK (status IN ('draft', 'sent', 'accepted', 'declined', 'expired')),
    priority VARCHAR(20) DEFAULT 'medium' CHECK (priority IN ('low', 'medium', 'high', 'urgent')),
    category VARCHAR(100),
    source VARCHAR(100),
    
    -- Addresses
    billing_address TEXT,
    shipping_address TEXT,
    
    -- Dates and Validity
    valid_from TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    valid_until TIMESTAMP WITH TIME ZONE,
    sent_at TIMESTAMP WITH TIME ZONE,
    accepted_at TIMESTAMP WITH TIME ZONE,
    declined_at TIMESTAMP WITH TIME ZONE,
    
    -- Assignment and Conversion
    assigned_to UUID REFERENCES users(id) ON DELETE SET NULL,
    converted_to_sale_id UUID,
    
    -- Sharing
    share_link VARCHAR(500),
    is_shared BOOLEAN DEFAULT FALSE,
    
    -- Recurring
    is_recurring BOOLEAN DEFAULT FALSE,
    recurring_interval VARCHAR(20) CHECK (recurring_interval IS NULL OR recurring_interval IN ('weekly', 'monthly', 'quarterly', 'annually')),
    next_renewal_date TIMESTAMP WITH TIME ZONE,
    
    -- Tags
    tags TEXT[],
    
    -- Audit Fields
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_activity TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    modified_by UUID REFERENCES users(id) ON DELETE SET NULL,
    
    -- Constraints
    CONSTRAINT valid_amount CHECK (amount >= 0),
    CONSTRAINT valid_tax CHECK (tax_amount >= 0),
    CONSTRAINT valid_discount CHECK (discount_amount >= 0)
);

-- =====================================================
-- 2. OFFERS TABLE INDEXES
-- =====================================================
CREATE INDEX IF NOT EXISTS idx_offers_contact_id ON offers(contact_id);
CREATE INDEX IF NOT EXISTS idx_offers_status ON offers(status);
CREATE INDEX IF NOT EXISTS idx_offers_category ON offers(category);
CREATE INDEX IF NOT EXISTS idx_offers_created_at ON offers(created_at);
CREATE INDEX IF NOT EXISTS idx_offers_valid_until ON offers(valid_until);
CREATE INDEX IF NOT EXISTS idx_offers_assigned_to ON offers(assigned_to);
CREATE INDEX IF NOT EXISTS idx_offers_created_by ON offers(created_by);
CREATE INDEX IF NOT EXISTS idx_offers_tags ON offers USING GIN(tags);

-- Full-text search index
CREATE INDEX IF NOT EXISTS idx_offers_search ON offers 
    USING GIN(to_tsvector('english', COALESCE(title, '') || ' ' || COALESCE(description, '')));

-- =====================================================
-- 3. OFFER ITEMS TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS offer_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    offer_id UUID NOT NULL REFERENCES offers(id) ON DELETE CASCADE,
    
    -- Item Information
    type VARCHAR(20) NOT NULL CHECK (type IN ('service', 'article')),
    article_id VARCHAR(50) NOT NULL,
    item_name VARCHAR(255) NOT NULL,
    item_code VARCHAR(100),
    description TEXT,
    
    -- Pricing
    quantity DECIMAL(18, 4) NOT NULL DEFAULT 1,
    unit_price DECIMAL(18, 2) NOT NULL,
    discount DECIMAL(18, 2) DEFAULT 0,
    discount_type VARCHAR(20) DEFAULT 'percentage' CHECK (discount_type IN ('percentage', 'fixed')),
    
    -- Calculated total price
    total_price DECIMAL(18, 2) GENERATED ALWAYS AS (
        CASE 
            WHEN discount_type = 'percentage' THEN 
                (quantity * unit_price) * (1 - (discount / 100))
            WHEN discount_type = 'fixed' THEN 
                (quantity * unit_price) - discount
            ELSE 
                quantity * unit_price
        END
    ) STORED,
    
    -- Position for ordering
    position INTEGER NOT NULL DEFAULT 0,
    
    -- Optional installation reference
    installation_id UUID,
    installation_name VARCHAR(255),
    
    -- Service order requirement
    requires_service_order BOOLEAN DEFAULT FALSE,
    
    -- Constraints
    CONSTRAINT valid_quantity CHECK (quantity > 0),
    CONSTRAINT valid_unit_price CHECK (unit_price >= 0),
    CONSTRAINT valid_discount CHECK (discount >= 0)
);

-- =====================================================
-- 4. OFFER ITEMS TABLE INDEXES
-- =====================================================
CREATE INDEX IF NOT EXISTS idx_offer_items_offer_id ON offer_items(offer_id);
CREATE INDEX IF NOT EXISTS idx_offer_items_type ON offer_items(type);
CREATE INDEX IF NOT EXISTS idx_offer_items_article_id ON offer_items(article_id);
CREATE INDEX IF NOT EXISTS idx_offer_items_position ON offer_items(offer_id, position);

-- =====================================================
-- 5. OFFER ACTIVITIES TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS offer_activities (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    offer_id UUID NOT NULL REFERENCES offers(id) ON DELETE CASCADE,
    
    -- Activity Information
    type VARCHAR(50) NOT NULL CHECK (type IN (
        'created', 'updated', 'status_changed', 'sent', 'viewed', 
        'accepted', 'declined', 'expired', 'note_added', 'item_added', 
        'item_updated', 'item_removed', 'assigned', 'converted', 'renewed'
    )),
    description TEXT NOT NULL,
    
    -- Change tracking
    old_value TEXT,
    new_value TEXT,
    
    -- Additional metadata
    metadata JSONB,
    
    -- Audit
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT
);

-- =====================================================
-- 6. OFFER ACTIVITIES TABLE INDEXES
-- =====================================================
CREATE INDEX IF NOT EXISTS idx_offer_activities_offer_id ON offer_activities(offer_id);
CREATE INDEX IF NOT EXISTS idx_offer_activities_type ON offer_activities(type);
CREATE INDEX IF NOT EXISTS idx_offer_activities_created_at ON offer_activities(created_at);
CREATE INDEX IF NOT EXISTS idx_offer_activities_created_by ON offer_activities(created_by);

-- =====================================================
-- 7. TRIGGERS
-- =====================================================

-- Trigger to auto-update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    NEW.last_activity = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER update_offers_updated_at
    BEFORE UPDATE ON offers
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Trigger to log status changes
CREATE OR REPLACE FUNCTION log_offer_status_change()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD.status IS DISTINCT FROM NEW.status THEN
        INSERT INTO offer_activities (offer_id, type, description, old_value, new_value, created_by)
        VALUES (
            NEW.id,
            'status_changed',
            'Status changed from ' || OLD.status || ' to ' || NEW.status,
            OLD.status,
            NEW.status,
            NEW.modified_by
        );
        
        -- Update timestamp fields based on status
        IF NEW.status = 'sent' AND NEW.sent_at IS NULL THEN
            NEW.sent_at = CURRENT_TIMESTAMP;
        ELSIF NEW.status = 'accepted' AND NEW.accepted_at IS NULL THEN
            NEW.accepted_at = CURRENT_TIMESTAMP;
        ELSIF NEW.status = 'declined' AND NEW.declined_at IS NULL THEN
            NEW.declined_at = CURRENT_TIMESTAMP;
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER log_offer_status_changes
    BEFORE UPDATE ON offers
    FOR EACH ROW
    EXECUTE FUNCTION log_offer_status_change();

-- Trigger to log offer creation
CREATE OR REPLACE FUNCTION log_offer_creation()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO offer_activities (offer_id, type, description, created_by)
    VALUES (
        NEW.id,
        'created',
        'Offer created: ' || NEW.title,
        NEW.created_by
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER log_new_offer
    AFTER INSERT ON offers
    FOR EACH ROW
    EXECUTE FUNCTION log_offer_creation();

-- Trigger to update offer amount when items change
CREATE OR REPLACE FUNCTION update_offer_amount_from_items()
RETURNS TRIGGER AS $$
DECLARE
    offer_total DECIMAL(18, 2);
BEGIN
    -- Calculate total from all items
    SELECT COALESCE(SUM(total_price), 0)
    INTO offer_total
    FROM offer_items
    WHERE offer_id = COALESCE(NEW.offer_id, OLD.offer_id);
    
    -- Update the offer amount
    UPDATE offers
    SET amount = offer_total,
        updated_at = CURRENT_TIMESTAMP
    WHERE id = COALESCE(NEW.offer_id, OLD.offer_id);
    
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER update_offer_amount_on_item_insert
    AFTER INSERT ON offer_items
    FOR EACH ROW
    EXECUTE FUNCTION update_offer_amount_from_items();

CREATE TRIGGER update_offer_amount_on_item_update
    AFTER UPDATE ON offer_items
    FOR EACH ROW
    EXECUTE FUNCTION update_offer_amount_from_items();

CREATE TRIGGER update_offer_amount_on_item_delete
    AFTER DELETE ON offer_items
    FOR EACH ROW
    EXECUTE FUNCTION update_offer_amount_from_items();

-- =====================================================
-- 8. HELPER FUNCTIONS
-- =====================================================

-- Function to get offer statistics
CREATE OR REPLACE FUNCTION get_offer_statistics(
    start_date TIMESTAMP WITH TIME ZONE DEFAULT NULL,
    end_date TIMESTAMP WITH TIME ZONE DEFAULT NULL
)
RETURNS TABLE (
    total_offers BIGINT,
    active_offers BIGINT,
    accepted_offers BIGINT,
    declined_offers BIGINT,
    total_value DECIMAL(18, 2),
    average_value DECIMAL(18, 2),
    conversion_rate DECIMAL(5, 2)
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        COUNT(*)::BIGINT as total_offers,
        COUNT(*) FILTER (WHERE status IN ('draft', 'sent'))::BIGINT as active_offers,
        COUNT(*) FILTER (WHERE status = 'accepted')::BIGINT as accepted_offers,
        COUNT(*) FILTER (WHERE status = 'declined')::BIGINT as declined_offers,
        COALESCE(SUM(total_amount), 0) as total_value,
        COALESCE(AVG(total_amount), 0) as average_value,
        CASE 
            WHEN COUNT(*) FILTER (WHERE status IN ('accepted', 'declined')) > 0 
            THEN (COUNT(*) FILTER (WHERE status = 'accepted')::DECIMAL / 
                  COUNT(*) FILTER (WHERE status IN ('accepted', 'declined'))::DECIMAL * 100)
            ELSE 0
        END as conversion_rate
    FROM offers
    WHERE (start_date IS NULL OR created_at >= start_date)
      AND (end_date IS NULL OR created_at <= end_date);
END;
$$ LANGUAGE plpgsql;

-- =====================================================
-- 9. GRANTS (Uncomment if needed)
-- =====================================================
-- GRANT SELECT, INSERT, UPDATE, DELETE ON offers TO api_user;
-- GRANT SELECT, INSERT, UPDATE, DELETE ON offer_items TO api_user;
-- GRANT SELECT, INSERT, UPDATE, DELETE ON offer_activities TO api_user;

-- =====================================================
-- MIGRATION COMPLETE
-- =====================================================
-- Execute this script in your Neon SQL Editor
-- All tables, indexes, triggers, and functions are now created
-- =====================================================
