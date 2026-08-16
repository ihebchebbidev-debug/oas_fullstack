-- =====================================================
-- Retenue à la Source (RS) & TEJ Export Tables
-- Run this migration on your PostgreSQL database
-- =====================================================

-- RS Records table
CREATE TABLE IF NOT EXISTS "RSRecords" (
    "Id"               SERIAL PRIMARY KEY,
    "EntityType"       VARCHAR(30)     NOT NULL,  -- 'offer', 'sale' or 'supplier_invoice'
    "EntityId"         INTEGER         NOT NULL,
    "EntityNumber"     VARCHAR(50),
    "InvoiceNumber"    VARCHAR(100)    NOT NULL,
    "InvoiceDate"      TIMESTAMP       NOT NULL,
    "InvoiceAmount"    DECIMAL(15,2)   NOT NULL,
    "PaymentDate"      TIMESTAMP       NOT NULL,
    "AmountPaid"       DECIMAL(15,2)   NOT NULL,
    "RSAmount"         DECIMAL(15,2)   NOT NULL,
    "RSTypeCode"       VARCHAR(10)     NOT NULL DEFAULT '10',
    "SupplierName"     VARCHAR(255)    NOT NULL,
    "SupplierTaxId"    VARCHAR(50)     NOT NULL,  -- Matricule Fiscal
    "SupplierAddress"  VARCHAR(500),
    "PayerName"        VARCHAR(255)    NOT NULL,
    "PayerTaxId"       VARCHAR(50)     NOT NULL,
    "PayerAddress"     VARCHAR(500),
    "Status"           VARCHAR(20)     NOT NULL DEFAULT 'pending',
    "TEJExported"      BOOLEAN         NOT NULL DEFAULT FALSE,
    "TEJFileName"      VARCHAR(255),
    "Notes"            VARCHAR(1000),
    "CreatedAt"        TIMESTAMP       NOT NULL DEFAULT NOW(),
    "CreatedBy"        VARCHAR(100)    NOT NULL,
    "ModifiedAt"       TIMESTAMP,
    "ModifiedBy"       VARCHAR(100)
);

-- Indexes for RSRecords
CREATE INDEX IF NOT EXISTS "IX_RSRecords_PaymentDate_Status"
    ON "RSRecords" ("PaymentDate", "Status");

CREATE INDEX IF NOT EXISTS "IX_RSRecords_EntityType_EntityId"
    ON "RSRecords" ("EntityType", "EntityId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_RSRecords_Invoice_Payment_Entity"
    ON "RSRecords" ("InvoiceNumber", "PaymentDate", "EntityId", "EntityType");

-- TEJ Export Logs table
CREATE TABLE IF NOT EXISTS "TEJExportLogs" (
    "Id"               SERIAL PRIMARY KEY,
    "FileName"         VARCHAR(255)    NOT NULL,
    "ExportDate"       TIMESTAMP       NOT NULL DEFAULT NOW(),
    "ExportedBy"       VARCHAR(100)    NOT NULL,
    "Month"            INTEGER         NOT NULL,
    "Year"             INTEGER         NOT NULL,
    "RecordCount"      INTEGER         NOT NULL DEFAULT 0,
    "TotalRSAmount"    DECIMAL(15,2)   NOT NULL DEFAULT 0,
    "Status"           VARCHAR(20)     NOT NULL DEFAULT 'success',
    "ErrorMessage"     VARCHAR(2000),
    "DocumentId"       INTEGER         REFERENCES "Documents"("Id") ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS "IX_TEJExportLogs_Year_Month"
    ON "TEJExportLogs" ("Year", "Month");

-- =====================================================
-- COMMENTS for documentation
-- =====================================================
COMMENT ON TABLE "RSRecords" IS 'Retenue à la Source (Withholding Tax) records for Tunisian fiscal compliance';
COMMENT ON COLUMN "RSRecords"."RSTypeCode" IS 'TEJ transaction type: 10=Services 10%, 05=Export 0.5%, 03=Prof Fees 3%, 20=Royalties 20%';
COMMENT ON COLUMN "RSRecords"."SupplierTaxId" IS 'Matricule Fiscal of the supplier/beneficiary';
COMMENT ON TABLE "TEJExportLogs" IS 'Audit log of TEJ XML file exports for tax authority submission';
