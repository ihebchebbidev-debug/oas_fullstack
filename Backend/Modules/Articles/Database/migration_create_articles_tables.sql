-- =====================================================
-- Articles Module - Database Migration Script
-- =====================================================
-- This script creates all tables for the Articles & Inventory module
-- including: articles, categories, locations, and inventory transactions

-- =====================================================
-- 1. ARTICLES TABLE
-- =====================================================
-- Main table for both materials and services
CREATE TABLE IF NOT EXISTS articles (
    -- Primary Key
    id VARCHAR(50) PRIMARY KEY,
    
    -- Basic Information
    type VARCHAR(20) NOT NULL CHECK (type IN ('material', 'service')),
    name VARCHAR(255) NOT NULL,
    sku VARCHAR(100),
    description TEXT,
    category VARCHAR(100) NOT NULL,
    status VARCHAR(50) NOT NULL CHECK (status IN ('available', 'low_stock', 'out_of_stock', 'discontinued', 'active', 'inactive')),
    
    -- Material-specific fields (nullable for services)
    stock INTEGER,
    min_stock INTEGER,
    cost_price DECIMAL(10, 2),
    sell_price DECIMAL(10, 2),
    supplier VARCHAR(255),
    location VARCHAR(255),
    sub_location VARCHAR(255),
    
    -- Service-specific fields (nullable for materials)
    base_price DECIMAL(10, 2),
    duration INTEGER, -- in minutes
    skills_required TEXT, -- JSON array stored as string
    materials_needed TEXT, -- JSON array stored as string
    preferred_users TEXT, -- JSON array stored as string
    
    -- Usage tracking
    last_used TIMESTAMP,
    last_used_by VARCHAR(50),
    
    -- Common fields
    tags TEXT, -- JSON array stored as string
    notes TEXT,
    
    -- Metadata
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by VARCHAR(50) NOT NULL,
    modified_by VARCHAR(50) NOT NULL,
    
    -- Constraints
    CONSTRAINT check_material_fields CHECK (
        type != 'material' OR (location IS NOT NULL AND sell_price IS NOT NULL)
    ),
    CONSTRAINT check_service_fields CHECK (
        type != 'service' OR (base_price IS NOT NULL AND duration IS NOT NULL)
    ),
    CONSTRAINT check_stock_non_negative CHECK (stock IS NULL OR stock >= 0),
    CONSTRAINT check_min_stock_non_negative CHECK (min_stock IS NULL OR min_stock >= 0),
    CONSTRAINT check_prices_non_negative CHECK (
        (cost_price IS NULL OR cost_price >= 0) AND
        (sell_price IS NULL OR sell_price >= 0) AND
        (base_price IS NULL OR base_price >= 0)
    )
);

-- =====================================================
-- 2. ARTICLE CATEGORIES TABLE
-- =====================================================
-- Manages article and service categories
CREATE TABLE IF NOT EXISTS article_categories (
    -- Primary Key
    id VARCHAR(50) PRIMARY KEY,
    
    -- Basic Information
    name VARCHAR(100) NOT NULL,
    type VARCHAR(20) NOT NULL CHECK (type IN ('material', 'service', 'both')),
    description VARCHAR(500),
    parent_id VARCHAR(50),
    
    -- Ordering and Status
    "order" INTEGER NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    
    -- Metadata
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Foreign Keys
    CONSTRAINT fk_parent_category FOREIGN KEY (parent_id) 
        REFERENCES article_categories(id) ON DELETE SET NULL
);

-- =====================================================
-- 3. LOCATIONS TABLE
-- =====================================================
-- Manages storage locations (warehouses, vehicles, etc.)
CREATE TABLE IF NOT EXISTS locations (
    -- Primary Key
    id VARCHAR(50) PRIMARY KEY,
    
    -- Basic Information
    name VARCHAR(255) NOT NULL,
    type VARCHAR(50) NOT NULL CHECK (type IN ('warehouse', 'vehicle', 'office', 'other')),
    address VARCHAR(500),
    
    -- Assignment and Capacity
    assigned_technician VARCHAR(50), -- For mobile locations like vehicles
    capacity INTEGER,
    
    -- Status
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    
    -- Metadata
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Constraints
    CONSTRAINT check_capacity_non_negative CHECK (capacity IS NULL OR capacity >= 0)
);

-- =====================================================
-- 4. INVENTORY TRANSACTIONS TABLE
-- =====================================================
-- Tracks all inventory movements and adjustments
CREATE TABLE IF NOT EXISTS inventory_transactions (
    -- Primary Key
    id VARCHAR(50) PRIMARY KEY,
    
    -- Transaction Information
    article_id VARCHAR(50) NOT NULL,
    type VARCHAR(20) NOT NULL CHECK (type IN ('in', 'out', 'transfer', 'adjustment')),
    quantity INTEGER NOT NULL,
    
    -- Location Information
    from_location VARCHAR(255),
    to_location VARCHAR(255),
    
    -- Transaction Details
    reason VARCHAR(500) NOT NULL,
    reference VARCHAR(100), -- Service order ID, purchase order, etc.
    performed_by VARCHAR(50) NOT NULL,
    notes TEXT,
    
    -- Metadata
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Foreign Keys
    CONSTRAINT fk_article FOREIGN KEY (article_id) 
        REFERENCES articles(id) ON DELETE CASCADE,
    
    -- Constraints
    CONSTRAINT check_quantity_positive CHECK (quantity > 0),
    CONSTRAINT check_transfer_locations CHECK (
        type != 'transfer' OR (from_location IS NOT NULL AND to_location IS NOT NULL)
    )
);

-- =====================================================
-- INDEXES
-- =====================================================

-- Articles Table Indexes
CREATE INDEX IF NOT EXISTS idx_articles_type ON articles(type);
CREATE INDEX IF NOT EXISTS idx_articles_category ON articles(category);
CREATE INDEX IF NOT EXISTS idx_articles_status ON articles(status);
CREATE INDEX IF NOT EXISTS idx_articles_location ON articles(location);
CREATE INDEX IF NOT EXISTS idx_articles_sku ON articles(sku);
CREATE INDEX IF NOT EXISTS idx_articles_created_at ON articles(created_at);
CREATE INDEX IF NOT EXISTS idx_articles_name ON articles(name);

-- Categories Table Indexes
CREATE INDEX IF NOT EXISTS idx_categories_type ON article_categories(type);
CREATE INDEX IF NOT EXISTS idx_categories_parent ON article_categories(parent_id);
CREATE INDEX IF NOT EXISTS idx_categories_active ON article_categories(is_active);
CREATE INDEX IF NOT EXISTS idx_categories_order ON article_categories("order");

-- Locations Table Indexes
CREATE INDEX IF NOT EXISTS idx_locations_type ON locations(type);
CREATE INDEX IF NOT EXISTS idx_locations_active ON locations(is_active);
CREATE INDEX IF NOT EXISTS idx_locations_technician ON locations(assigned_technician);

-- Transactions Table Indexes
CREATE INDEX IF NOT EXISTS idx_transactions_article ON inventory_transactions(article_id);
CREATE INDEX IF NOT EXISTS idx_transactions_type ON inventory_transactions(type);
CREATE INDEX IF NOT EXISTS idx_transactions_created_at ON inventory_transactions(created_at);
CREATE INDEX IF NOT EXISTS idx_transactions_reference ON inventory_transactions(reference);
CREATE INDEX IF NOT EXISTS idx_transactions_performed_by ON inventory_transactions(performed_by);

-- =====================================================
-- TRIGGERS
-- =====================================================

-- Trigger to automatically update updated_at timestamp on articles
CREATE OR REPLACE FUNCTION update_articles_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_update_articles_timestamp
    BEFORE UPDATE ON articles
    FOR EACH ROW
    EXECUTE FUNCTION update_articles_timestamp();

-- =====================================================
-- INITIAL DATA - SAMPLE CATEGORIES
-- =====================================================

INSERT INTO article_categories (id, name, type, description, "order", is_active) VALUES
    ('CAT-TOOLS', 'Tools', 'material', 'Hand tools and power tools', 1, TRUE),
    ('CAT-ELEC', 'Electrical', 'both', 'Electrical components and services', 2, TRUE),
    ('CAT-PLUMB', 'Plumbing', 'both', 'Plumbing materials and services', 3, TRUE),
    ('CAT-SAFE', 'Safety', 'material', 'Safety equipment and gear', 4, TRUE),
    ('CAT-HARD', 'Hardware', 'material', 'General hardware items', 5, TRUE),
    ('CAT-MAT', 'Materials', 'material', 'Construction and repair materials', 6, TRUE),
    ('CAT-EQUIP', 'Equipment', 'material', 'Heavy equipment and machinery', 7, TRUE),
    ('CAT-AUTO', 'Automotive', 'both', 'Automotive parts and services', 8, TRUE),
    ('CAT-HVAC', 'HVAC', 'service', 'Heating, ventilation, and air conditioning', 9, TRUE),
    ('CAT-APPL', 'Appliance Repair', 'service', 'Appliance repair services', 10, TRUE),
    ('CAT-MAINT', 'Maintenance', 'service', 'General maintenance services', 11, TRUE),
    ('CAT-INST', 'Installation', 'service', 'Installation services', 12, TRUE),
    ('CAT-DIAG', 'Diagnostic', 'service', 'Diagnostic and inspection services', 13, TRUE),
    ('CAT-OTHER', 'Other', 'both', 'Miscellaneous items and services', 99, TRUE)
ON CONFLICT (id) DO NOTHING;

-- =====================================================
-- INITIAL DATA - SAMPLE LOCATIONS
-- =====================================================

INSERT INTO locations (id, name, type, is_active) VALUES
    ('LOC-WHA', 'Warehouse A', 'warehouse', TRUE),
    ('LOC-WHB', 'Warehouse B', 'warehouse', TRUE),
    ('LOC-VAN1', 'Service Van 1', 'vehicle', TRUE),
    ('LOC-VAN2', 'Service Van 2', 'vehicle', TRUE),
    ('LOC-OFF', 'Main Office', 'office', TRUE)
ON CONFLICT (id) DO NOTHING;

-- =====================================================
-- COMMENTS
-- =====================================================

COMMENT ON TABLE articles IS 'Unified table for materials (inventory items) and services';
COMMENT ON TABLE article_categories IS 'Categories for organizing articles and services';
COMMENT ON TABLE locations IS 'Physical locations for storing inventory items';
COMMENT ON TABLE inventory_transactions IS 'Transaction history for inventory movements';

COMMENT ON COLUMN articles.type IS 'Type of article: material or service';
COMMENT ON COLUMN articles.skills_required IS 'JSON array of required skills for services';
COMMENT ON COLUMN articles.materials_needed IS 'JSON array of materials needed for services';
COMMENT ON COLUMN articles.preferred_users IS 'JSON array of preferred technician IDs';
COMMENT ON COLUMN articles.tags IS 'JSON array of tags for categorization';

COMMENT ON COLUMN inventory_transactions.type IS 'Transaction type: in (receive), out (use), transfer (move), adjustment (correct)';
COMMENT ON COLUMN inventory_transactions.reference IS 'Reference to related entity (e.g., service order ID, purchase order ID)';

-- =====================================================
-- END OF MIGRATION SCRIPT
-- =====================================================
