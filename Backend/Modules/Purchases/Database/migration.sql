-- =====================================================
-- Purchase Module — Complete Database Migration
-- Tables: ArticleSuppliers, ArticleSupplierPriceHistory,
--         PurchaseOrders, PurchaseOrderItems,
--         GoodsReceipts, GoodsReceiptItems,
--         SupplierInvoices, SupplierInvoiceItems,
--         PurchaseActivities
-- =====================================================

-- ─── 1. ArticleSuppliers ────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "ArticleSuppliers" (
    "Id"               SERIAL PRIMARY KEY,
    "TenantId"         INTEGER         NOT NULL,
    "ArticleId"        INTEGER         NOT NULL REFERENCES "Articles"("Id") ON DELETE CASCADE,
    "SupplierId"       INTEGER         NOT NULL REFERENCES "Contacts"("Id") ON DELETE RESTRICT,
    "SupplierRef"      VARCHAR(100),
    "PurchasePrice"    DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "Currency"         VARCHAR(3)      NOT NULL DEFAULT 'TND',
    "MinOrderQty"      DECIMAL(18,2)   NOT NULL DEFAULT 1,
    "LeadTimeDays"     INTEGER         NOT NULL DEFAULT 0,
    "IsPreferred"      BOOLEAN         NOT NULL DEFAULT FALSE,
    "IsActive"         BOOLEAN         NOT NULL DEFAULT TRUE,
    "Notes"            TEXT,
    "CreatedDate"      TIMESTAMP       NOT NULL DEFAULT NOW(),
    "CreatedBy"        VARCHAR(100)    NOT NULL,
    "ModifiedDate"     TIMESTAMP,
    "ModifiedBy"       VARCHAR(100)
);

CREATE INDEX IF NOT EXISTS "IX_ArticleSuppliers_TenantId" ON "ArticleSuppliers" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_ArticleSuppliers_ArticleId" ON "ArticleSuppliers" ("ArticleId");
CREATE INDEX IF NOT EXISTS "IX_ArticleSuppliers_SupplierId" ON "ArticleSuppliers" ("SupplierId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ArticleSuppliers_Unique" ON "ArticleSuppliers" ("TenantId", "ArticleId", "SupplierId");

-- ─── 2. ArticleSupplierPriceHistory ─────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "ArticleSupplierPriceHistory" (
    "Id"                  SERIAL PRIMARY KEY,
    "TenantId"            INTEGER         NOT NULL,
    "ArticleSupplierId"   INTEGER         NOT NULL REFERENCES "ArticleSuppliers"("Id") ON DELETE CASCADE,
    "OldPrice"            DECIMAL(18,2)   NOT NULL,
    "NewPrice"            DECIMAL(18,2)   NOT NULL,
    "Currency"            VARCHAR(3)      NOT NULL DEFAULT 'TND',
    "ChangedAt"           TIMESTAMP       NOT NULL DEFAULT NOW(),
    "ChangedBy"           VARCHAR(100)    NOT NULL,
    "Reason"              VARCHAR(500)
);

CREATE INDEX IF NOT EXISTS "IX_ArticleSupplierPriceHistory_TenantId" ON "ArticleSupplierPriceHistory" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_ArticleSupplierPriceHistory_ArticleSupplierId" ON "ArticleSupplierPriceHistory" ("ArticleSupplierId");

-- ─── 3. PurchaseOrders ──────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "PurchaseOrders" (
    "Id"                        SERIAL PRIMARY KEY,
    "TenantId"                  INTEGER         NOT NULL,
    "OrderNumber"               VARCHAR(50)     NOT NULL,
    "Title"                     VARCHAR(255),
    "Description"               TEXT,
    "SupplierId"                INTEGER         NOT NULL REFERENCES "Contacts"("Id"),
    "SupplierName"              VARCHAR(255)    NOT NULL DEFAULT '',
    "SupplierEmail"             VARCHAR(255),
    "SupplierPhone"             VARCHAR(20),
    "SupplierAddress"           VARCHAR(500),
    "SupplierMatriculeFiscale"  VARCHAR(100),
    "Status"                    VARCHAR(30)     NOT NULL DEFAULT 'draft',
    "OrderDate"                 TIMESTAMP       NOT NULL DEFAULT NOW(),
    "ExpectedDelivery"          TIMESTAMP,
    "ActualDelivery"            TIMESTAMP,
    "Currency"                  VARCHAR(3)      NOT NULL DEFAULT 'TND',
    "SubTotal"                  DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "Discount"                  DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "DiscountType"              VARCHAR(20)     NOT NULL DEFAULT 'percentage',
    "TaxAmount"                 DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "FiscalStamp"               DECIMAL(10,3)   NOT NULL DEFAULT 1.000,
    "GrandTotal"                DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "PaymentTerms"              VARCHAR(50)     DEFAULT 'net30',
    "PaymentStatus"             VARCHAR(20)     NOT NULL DEFAULT 'pending',
    "Notes"                     TEXT,
    "Tags"                      TEXT[],
    "BillingAddress"            TEXT,
    "DeliveryAddress"           TEXT,
    "ServiceOrderId"            VARCHAR(50),
    "SaleId"                    VARCHAR(50),
    "ApprovedBy"                VARCHAR(100),
    "ApprovalDate"              TIMESTAMP,
    "SentToSupplierAt"          TIMESTAMP,
    "CreatedDate"               TIMESTAMP       NOT NULL DEFAULT NOW(),
    "CreatedBy"                 VARCHAR(100)    NOT NULL,
    "CreatedByName"             VARCHAR(255),
    "ModifiedDate"              TIMESTAMP,
    "ModifiedBy"                VARCHAR(100),
    "IsDeleted"                 BOOLEAN         NOT NULL DEFAULT FALSE,
    "DeletedAt"                 TIMESTAMP,
    "DeletedBy"                 VARCHAR(100)
);

CREATE INDEX IF NOT EXISTS "IX_PurchaseOrders_TenantId" ON "PurchaseOrders" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_PurchaseOrders_SupplierId" ON "PurchaseOrders" ("SupplierId");
CREATE INDEX IF NOT EXISTS "IX_PurchaseOrders_Status" ON "PurchaseOrders" ("TenantId", "Status");
CREATE INDEX IF NOT EXISTS "IX_PurchaseOrders_OrderDate" ON "PurchaseOrders" ("TenantId", "OrderDate" DESC);

-- ─── 4. PurchaseOrderItems ──────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "PurchaseOrderItems" (
    "Id"               SERIAL PRIMARY KEY,
    "TenantId"         INTEGER         NOT NULL,
    "PurchaseOrderId"  INTEGER         NOT NULL REFERENCES "PurchaseOrders"("Id") ON DELETE CASCADE,
    "ArticleId"        INTEGER         REFERENCES "Articles"("Id"),
    "ArticleName"      VARCHAR(255),
    "ArticleNumber"    VARCHAR(50),
    "SupplierRef"      VARCHAR(100),
    "Description"      VARCHAR(500)    NOT NULL DEFAULT '',
    "Quantity"         DECIMAL(18,2)   NOT NULL DEFAULT 1,
    "ReceivedQty"      DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "UnitPrice"        DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "TaxRate"          DECIMAL(5,2)    NOT NULL DEFAULT 19,
    "Discount"         DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "DiscountType"     VARCHAR(20)     NOT NULL DEFAULT 'percentage',
    "LineTotal"        DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "Unit"             VARCHAR(20)     NOT NULL DEFAULT 'piece',
    "DisplayOrder"     INTEGER         NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS "IX_PurchaseOrderItems_TenantId" ON "PurchaseOrderItems" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_PurchaseOrderItems_PurchaseOrderId" ON "PurchaseOrderItems" ("PurchaseOrderId");

-- ─── 5. GoodsReceipts ───────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "GoodsReceipts" (
    "Id"               SERIAL PRIMARY KEY,
    "TenantId"         INTEGER         NOT NULL,
    "ReceiptNumber"    VARCHAR(50)     NOT NULL,
    "PurchaseOrderId"  INTEGER         NOT NULL REFERENCES "PurchaseOrders"("Id"),
    "SupplierId"       INTEGER         NOT NULL REFERENCES "Contacts"("Id"),
    "SupplierName"     VARCHAR(255)    NOT NULL DEFAULT '',
    "ReceiptDate"      TIMESTAMP       NOT NULL DEFAULT NOW(),
    "Status"           VARCHAR(20)     NOT NULL DEFAULT 'partial',
    "DeliveryNoteRef"  VARCHAR(100),
    "Notes"            TEXT,
    "ReceivedBy"       VARCHAR(100)    NOT NULL,
    "ReceivedByName"   VARCHAR(255),
    "CreatedDate"      TIMESTAMP       NOT NULL DEFAULT NOW(),
    "CreatedBy"        VARCHAR(100)    NOT NULL,
    "ModifiedDate"     TIMESTAMP,
    "ModifiedBy"       VARCHAR(100)
);

CREATE INDEX IF NOT EXISTS "IX_GoodsReceipts_TenantId" ON "GoodsReceipts" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_GoodsReceipts_PurchaseOrderId" ON "GoodsReceipts" ("PurchaseOrderId");

-- ─── 6. GoodsReceiptItems ───────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "GoodsReceiptItems" (
    "Id"                    SERIAL PRIMARY KEY,
    "TenantId"              INTEGER         NOT NULL,
    "GoodsReceiptId"        INTEGER         NOT NULL REFERENCES "GoodsReceipts"("Id") ON DELETE CASCADE,
    "PurchaseOrderItemId"   INTEGER         NOT NULL REFERENCES "PurchaseOrderItems"("Id"),
    "ArticleId"             INTEGER         REFERENCES "Articles"("Id"),
    "ArticleName"           VARCHAR(255),
    "ArticleNumber"         VARCHAR(50),
    "OrderedQty"            DECIMAL(18,2)   NOT NULL,
    "QuantityReceived"      DECIMAL(18,2)   NOT NULL,
    "QuantityRejected"      DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "RejectionReason"       VARCHAR(500),
    "LocationId"            INTEGER,
    "Notes"                 VARCHAR(500)
);

CREATE INDEX IF NOT EXISTS "IX_GoodsReceiptItems_TenantId" ON "GoodsReceiptItems" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_GoodsReceiptItems_GoodsReceiptId" ON "GoodsReceiptItems" ("GoodsReceiptId");

-- ─── 7. SupplierInvoices ────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "SupplierInvoices" (
    "Id"                        SERIAL PRIMARY KEY,
    "TenantId"                  INTEGER         NOT NULL,
    "InvoiceNumber"             VARCHAR(50)     NOT NULL,
    "SupplierInvoiceRef"        VARCHAR(100),
    "SupplierId"                INTEGER         NOT NULL REFERENCES "Contacts"("Id"),
    "SupplierName"              VARCHAR(255)    NOT NULL DEFAULT '',
    "SupplierMatriculeFiscale"  VARCHAR(100),
    "PurchaseOrderId"           INTEGER         REFERENCES "PurchaseOrders"("Id"),
    "GoodsReceiptId"            INTEGER         REFERENCES "GoodsReceipts"("Id"),
    "InvoiceDate"               TIMESTAMP       NOT NULL DEFAULT NOW(),
    "DueDate"                   TIMESTAMP       NOT NULL,
    "Status"                    VARCHAR(20)     NOT NULL DEFAULT 'draft',
    "Currency"                  VARCHAR(3)      NOT NULL DEFAULT 'TND',
    "SubTotal"                  DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "Discount"                  DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "DiscountType"              VARCHAR(20)     NOT NULL DEFAULT 'percentage',
    "TaxAmount"                 DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "FiscalStamp"               DECIMAL(10,3)   NOT NULL DEFAULT 1.000,
    "GrandTotal"                DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "AmountPaid"                DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "PaymentMethod"             VARCHAR(50),
    "PaymentDate"               TIMESTAMP,
    "Notes"                     TEXT,
    -- Retenue à la Source
    "RsApplicable"              BOOLEAN         NOT NULL DEFAULT FALSE,
    "RsTypeCode"                VARCHAR(10),
    "RsAmount"                  DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "RsRecordId"                INTEGER         REFERENCES "RSRecords"("Id") ON DELETE SET NULL,
    -- Facture en Ligne
    "FactureEnLigneId"          VARCHAR(100),
    "FactureEnLigneStatus"      VARCHAR(20),
    "FactureEnLigneSentAt"      TIMESTAMP,
    -- TEJ Integration
    "TejSynced"                 BOOLEAN         NOT NULL DEFAULT FALSE,
    "TejSyncDate"               TIMESTAMP,
    "TejSyncStatus"             VARCHAR(20)     DEFAULT 'pending',
    "TejErrorMessage"           VARCHAR(2000),
    -- Meta
    "CreatedDate"               TIMESTAMP       NOT NULL DEFAULT NOW(),
    "CreatedBy"                 VARCHAR(100)    NOT NULL,
    "ModifiedDate"              TIMESTAMP,
    "ModifiedBy"                VARCHAR(100),
    "IsDeleted"                 BOOLEAN         NOT NULL DEFAULT FALSE,
    "DeletedAt"                 TIMESTAMP,
    "DeletedBy"                 VARCHAR(100)
);

CREATE INDEX IF NOT EXISTS "IX_SupplierInvoices_TenantId" ON "SupplierInvoices" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_SupplierInvoices_SupplierId" ON "SupplierInvoices" ("SupplierId");
CREATE INDEX IF NOT EXISTS "IX_SupplierInvoices_Status" ON "SupplierInvoices" ("TenantId", "Status");
CREATE INDEX IF NOT EXISTS "IX_SupplierInvoices_PurchaseOrderId" ON "SupplierInvoices" ("PurchaseOrderId");
CREATE INDEX IF NOT EXISTS "IX_SupplierInvoices_DueDate" ON "SupplierInvoices" ("TenantId", "DueDate");

-- ─── 8. SupplierInvoiceItems ────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "SupplierInvoiceItems" (
    "Id"                    SERIAL PRIMARY KEY,
    "TenantId"              INTEGER         NOT NULL,
    "SupplierInvoiceId"     INTEGER         NOT NULL REFERENCES "SupplierInvoices"("Id") ON DELETE CASCADE,
    "PurchaseOrderItemId"   INTEGER         REFERENCES "PurchaseOrderItems"("Id"),
    "ArticleId"             INTEGER         REFERENCES "Articles"("Id"),
    "ArticleName"           VARCHAR(255),
    "Description"           VARCHAR(500)    NOT NULL DEFAULT '',
    "Quantity"              DECIMAL(18,2)   NOT NULL DEFAULT 1,
    "UnitPrice"             DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "TaxRate"               DECIMAL(5,2)    NOT NULL DEFAULT 19,
    "LineTotal"             DECIMAL(18,2)   NOT NULL DEFAULT 0,
    "DisplayOrder"          INTEGER         NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS "IX_SupplierInvoiceItems_TenantId" ON "SupplierInvoiceItems" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_SupplierInvoiceItems_SupplierInvoiceId" ON "SupplierInvoiceItems" ("SupplierInvoiceId");

-- ─── 9. PurchaseActivities ──────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "PurchaseActivities" (
    "Id"               SERIAL PRIMARY KEY,
    "TenantId"         INTEGER         NOT NULL,
    "EntityType"       VARCHAR(30)     NOT NULL,
    "EntityId"         INTEGER         NOT NULL,
    "ActivityType"     VARCHAR(50)     NOT NULL,
    "Description"      TEXT,
    "OldValue"         VARCHAR(500),
    "NewValue"         VARCHAR(500),
    "PerformedBy"      VARCHAR(100)    NOT NULL,
    "PerformedByName"  VARCHAR(255),
    "PerformedAt"      TIMESTAMP       NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_PurchaseActivities_TenantId" ON "PurchaseActivities" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_PurchaseActivities_Entity" ON "PurchaseActivities" ("TenantId", "EntityType", "EntityId");
CREATE INDEX IF NOT EXISTS "IX_PurchaseActivities_PerformedAt" ON "PurchaseActivities" ("TenantId", "PerformedAt" DESC);

-- ─── Comments ───────────────────────────────────────────────────────────────

COMMENT ON TABLE "ArticleSuppliers" IS 'Links articles to supplier contacts with pricing, MOQ, and lead time';
COMMENT ON TABLE "ArticleSupplierPriceHistory" IS 'Audit trail of purchase price changes per article-supplier pair';
COMMENT ON TABLE "PurchaseOrders" IS 'Purchase order headers — tracks procurement from draft to received';
COMMENT ON TABLE "PurchaseOrderItems" IS 'Line items for purchase orders with ordered vs received quantities';
COMMENT ON TABLE "GoodsReceipts" IS 'Goods receipt headers — records delivery acceptance against POs';
COMMENT ON TABLE "GoodsReceiptItems" IS 'Line items for goods receipts with accepted/rejected quantities';
COMMENT ON TABLE "SupplierInvoices" IS 'Supplier invoice headers with RS, Facture en Ligne, and TEJ compliance';
COMMENT ON TABLE "SupplierInvoiceItems" IS 'Line items for supplier invoices';
COMMENT ON TABLE "PurchaseActivities" IS 'Audit trail for all purchase module operations';

-- ─── Additive patches (idempotent) ──────────────────────────────────────────
-- Columns that exist on the EF models but were missing from the original DDL.
-- Running this file repeatedly is safe.

-- ArticleSuppliers soft-delete (ArticleSupplierService filters on !IsDeleted)
ALTER TABLE "ArticleSuppliers" ADD COLUMN IF NOT EXISTS "IsDeleted"  BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE "ArticleSuppliers" ADD COLUMN IF NOT EXISTS "DeletedAt"  TIMESTAMP;
ALTER TABLE "ArticleSuppliers" ADD COLUMN IF NOT EXISTS "DeletedBy"  VARCHAR(100);

-- GoodsReceipts soft-delete (GoodsReceiptService filters on !IsDeleted)
ALTER TABLE "GoodsReceipts" ADD COLUMN IF NOT EXISTS "IsDeleted"  BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE "GoodsReceipts" ADD COLUMN IF NOT EXISTS "DeletedAt"  TIMESTAMP;
ALTER TABLE "GoodsReceipts" ADD COLUMN IF NOT EXISTS "DeletedBy"  VARCHAR(100);

-- The unique pair must only apply to live rows, otherwise a soft-deleted link
-- blocks re-adding the same article/supplier pair.
DROP INDEX IF EXISTS "IX_ArticleSuppliers_Unique";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ArticleSuppliers_Unique_Live"
    ON "ArticleSuppliers" ("TenantId", "ArticleId", "SupplierId") WHERE "IsDeleted" = FALSE;

-- Idempotency keys used by Create*Async to de-duplicate retried POSTs
ALTER TABLE "PurchaseOrders"   ADD COLUMN IF NOT EXISTS "IdempotencyKey" VARCHAR(100);
ALTER TABLE "GoodsReceipts"    ADD COLUMN IF NOT EXISTS "IdempotencyKey" VARCHAR(100);
ALTER TABLE "SupplierInvoices" ADD COLUMN IF NOT EXISTS "IdempotencyKey" VARCHAR(100);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_PurchaseOrders_IdempotencyKey"
    ON "PurchaseOrders" ("TenantId", "IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "UX_GoodsReceipts_IdempotencyKey"
    ON "GoodsReceipts" ("TenantId", "IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "UX_SupplierInvoices_IdempotencyKey"
    ON "SupplierInvoices" ("TenantId", "IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;
