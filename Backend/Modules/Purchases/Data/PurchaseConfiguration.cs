using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Purchases.Models;
using MyApi.Modules.Shared.Data.Configurations;

namespace MyApi.Modules.Purchases.Data
{
    public class PurchaseOrderConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PurchaseOrder>(entity =>
            {
                entity.ToTable("PurchaseOrders");
                entity.HasKey(e => e.Id);
                entity.HasMany(e => e.Items).WithOne(i => i.PurchaseOrder).HasForeignKey(i => i.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
                // NOTE: PurchaseActivity.EntityId is POLYMORPHIC — it points to a
                // purchase_order OR goods_receipt OR supplier_invoice depending on
                // PurchaseActivity.EntityType. It is NOT a foreign key to PurchaseOrders.
                // Mapping it as a HasMany().WithOne().HasForeignKey("EntityId") corrupts
                // EF's model: activities for goods receipts / supplier invoices end up
                // treated as orphan children of PurchaseOrders, and SaveChanges fails
                // with relationship-fixup errors on every write that logs an activity
                // (Create / Update / Delete PO, GR, Invoice).
                // The Activities navigation is unused — leave it un-mapped via
                // [NotMapped] on the model. Use _context.PurchaseActivities directly.
                entity.Ignore(e => e.Activities);
            });
        }
    }

    public class PurchaseOrderItemConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PurchaseOrderItem>(entity =>
            {
                entity.ToTable("PurchaseOrderItems");
                entity.HasKey(e => e.Id);
            });
        }
    }

    public class GoodsReceiptConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GoodsReceipt>(entity =>
            {
                entity.ToTable("GoodsReceipts");
                entity.HasKey(e => e.Id);
                entity.HasMany(e => e.Items).WithOne(i => i.GoodsReceipt).HasForeignKey(i => i.GoodsReceiptId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }

    public class GoodsReceiptItemConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GoodsReceiptItem>(entity =>
            {
                entity.ToTable("GoodsReceiptItems");
                entity.HasKey(e => e.Id);
            });
        }
    }

    public class SupplierInvoiceConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SupplierInvoice>(entity =>
            {
                entity.ToTable("SupplierInvoices");
                entity.HasKey(e => e.Id);
                entity.HasMany(e => e.Items).WithOne(i => i.SupplierInvoice).HasForeignKey(i => i.SupplierInvoiceId).OnDelete(DeleteBehavior.Cascade);

                // NOTE: SupplierId / PurchaseOrderId / GoodsReceiptId / RsRecordId are
                // plain INTEGER columns (matching the canonical migrations + the
                // RsRecordId FK to RSRecords.Id). A previous varchar value-converter
                // shim here was never registered (dead code) and conflicted with the
                // real integer schema, so it was removed. Any database that drifted
                // these columns to varchar is repaired by migration
                // Modules/Purchases/Database/migration.sql (column types are also reconciled at
                // startup by Infrastructure/DatabaseSchemaSynchronizer).
            });
        }
    }

    public class SupplierInvoiceItemConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SupplierInvoiceItem>(entity =>
            {
                entity.ToTable("SupplierInvoiceItems");
                entity.HasKey(e => e.Id);
            });
        }
    }

    public class ArticleSupplierConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ArticleSupplier>(entity =>
            {
                entity.ToTable("ArticleSuppliers");
                entity.HasKey(e => e.Id);
                // Restrict (was Cascade): we now soft-delete ArticleSupplier so price
                // history must remain intact. Restrict prevents a future hard delete
                // from silently dropping the historical price audit trail.
                entity.HasMany(e => e.PriceHistory).WithOne(h => h.ArticleSupplier).HasForeignKey(h => h.ArticleSupplierId).OnDelete(DeleteBehavior.Restrict);
            });
        }
    }

    public class ArticleSupplierPriceHistoryConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ArticleSupplierPriceHistory>(entity =>
            {
                entity.ToTable("ArticleSupplierPriceHistory");
                entity.HasKey(e => e.Id);
            });
        }
    }

    public class PurchaseActivityConfiguration : IEntityConfiguration
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PurchaseActivity>(entity =>
            {
                entity.ToTable("PurchaseActivities");
                entity.HasKey(e => e.Id);
            });
        }
    }
}
