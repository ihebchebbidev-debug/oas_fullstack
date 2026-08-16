using System;
using System.Collections.Generic;
using System.Linq;
using MyApi.Modules.Sales.Models;

namespace MyApi.Modules.Sales.Services
{
    /// <summary>
    /// Single source of truth for Sale / SaleItem money math.
    ///
    /// Mirrors the frontend rule in <c>src/lib/calculateTotal.ts</c>:
    ///   Subtotal → Discount → Tax (on the amount AFTER discount) → Fiscal stamp.
    ///
    /// Every write path (create, update, add item, update item, delete item,
    /// offer conversion) must run through here so persisted totals are never
    /// zero or stale, and so invoices generated from a sale inherit correct
    /// amounts.
    /// </summary>
    public static class SaleTotalsCalculator
    {
        public const int MoneyScale = 2;

        public static decimal Round(decimal value) =>
            Math.Round(value, MoneyScale, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Line total = quantity × unit price, minus the line discount
        /// (percentage or fixed). Never negative.
        /// </summary>
        public static decimal ComputeLineTotal(
            decimal quantity,
            decimal unitPrice,
            decimal? discount,
            string? discountType)
        {
            var gross = quantity * unitPrice;
            var d = discount ?? 0m;
            decimal discountAmount = 0m;
            if (d > 0m)
            {
                discountAmount = string.Equals(discountType, "percentage", StringComparison.OrdinalIgnoreCase)
                    ? gross * (d / 100m)
                    : d;
            }
            var net = gross - discountAmount;
            if (net < 0m) net = 0m;
            return Round(net);
        }

        /// <summary>Recomputes and assigns <see cref="SaleItem.LineTotal"/>.</summary>
        public static decimal ApplyLineTotal(SaleItem item)
        {
            item.LineTotal = ComputeLineTotal(item.Quantity, item.UnitPrice, item.Discount, item.DiscountType);
            return item.LineTotal;
        }

        public sealed class SaleTotals
        {
            public decimal Subtotal { get; init; }
            public decimal DiscountAmount { get; init; }
            public decimal AfterDiscount { get; init; }
            public decimal TaxAmount { get; init; }
            public decimal FiscalStamp { get; init; }
            public decimal GrandTotal { get; init; }
            /// <summary>Effective tax rate (%) implied by the sale's tax settings — used to spread header tax onto invoice lines.</summary>
            public decimal EffectiveTaxRate { get; init; }
        }

        public static SaleTotals Compute(
            decimal subtotal,
            decimal? discount,
            string? discountType,
            decimal? taxes,
            string? taxType,
            decimal? fiscalStamp)
        {
            subtotal = Round(subtotal);

            var d = discount ?? 0m;
            decimal discountAmount = 0m;
            if (d > 0m)
            {
                discountAmount = string.Equals(discountType, "percentage", StringComparison.OrdinalIgnoreCase)
                    ? subtotal * (d / 100m)
                    : d;
            }
            discountAmount = Round(Math.Min(discountAmount, subtotal));

            var afterDiscount = Round(subtotal - discountAmount);

            var t = taxes ?? 0m;
            decimal taxAmount = 0m;
            if (t > 0m)
            {
                taxAmount = string.Equals(taxType, "percentage", StringComparison.OrdinalIgnoreCase)
                    ? afterDiscount * (t / 100m)
                    : t;
            }
            taxAmount = Round(taxAmount);

            var stamp = Round(fiscalStamp ?? 0m);
            var grand = Round(afterDiscount + taxAmount + stamp);

            var effectiveRate = afterDiscount > 0m
                ? Math.Round(taxAmount / afterDiscount * 100m, 4, MidpointRounding.AwayFromZero)
                : 0m;

            return new SaleTotals
            {
                Subtotal = subtotal,
                DiscountAmount = discountAmount,
                AfterDiscount = afterDiscount,
                TaxAmount = taxAmount,
                FiscalStamp = stamp,
                GrandTotal = grand,
                EffectiveTaxRate = effectiveRate,
            };
        }

        /// <summary>
        /// The sale header has no DiscountType column; a non-null DiscountPercent
        /// is the marker that <see cref="Sale.Discount"/> is a percentage.
        /// </summary>
        public static string HeaderDiscountType(Sale sale) =>
            sale.DiscountPercent.HasValue ? "percentage" : "fixed";

        /// <summary>
        /// Recomputes every line total and the sale header totals, assigning
        /// <see cref="Sale.TotalAmount"/> (subtotal), <see cref="Sale.TaxAmount"/>
        /// and <see cref="Sale.GrandTotal"/>.
        /// </summary>
        public static SaleTotals Apply(Sale sale, IEnumerable<SaleItem>? items)
        {
            var list = (items ?? sale.Items ?? Enumerable.Empty<SaleItem>()).ToList();
            foreach (var item in list) ApplyLineTotal(item);

            var subtotal = list.Sum(i => i.LineTotal);

            // Sales with no line items keep their manually entered header amount.
            if (list.Count == 0) subtotal = sale.TotalAmount;

            // An empty sale with no manual amount is worth nothing — don't let a
            // fixed tax or fiscal stamp conjure a total out of thin air.
            if (subtotal <= 0m && list.Count == 0)
            {
                sale.TotalAmount = 0m;
                sale.DiscountAmount = 0m;
                sale.TaxAmount = 0m;
                sale.GrandTotal = 0m;
                return Compute(0m, 0m, "fixed", 0m, "fixed", 0m);
            }

            var totals = Compute(subtotal, sale.Discount, HeaderDiscountType(sale), sale.Taxes, sale.TaxType, sale.FiscalStamp);

            sale.TotalAmount = totals.Subtotal;
            sale.DiscountAmount = totals.DiscountAmount;
            sale.TaxAmount = totals.TaxAmount;
            sale.GrandTotal = totals.GrandTotal;
            return totals;
        }
    }
}
