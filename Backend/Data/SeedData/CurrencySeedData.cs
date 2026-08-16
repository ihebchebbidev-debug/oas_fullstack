using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Lookups.Models;

namespace MyApi.Data.SeedData
{
    public class CurrencySeedData
    {
        public void Seed(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Currency>().HasData(
                new Currency { Id = 1, Code = "USD", Name = "US Dollar", Symbol = "$", IsActive = true },
                new Currency { Id = 2, Code = "EUR", Name = "Euro", Symbol = "€", IsActive = true },
                new Currency { Id = 3, Code = "GBP", Name = "British Pound", Symbol = "£", IsActive = true },
                new Currency { Id = 4, Code = "CAD", Name = "Canadian Dollar", Symbol = "C$", IsActive = true },
                new Currency { Id = 5, Code = "AUD", Name = "Australian Dollar", Symbol = "A$", IsActive = true }
            );
        }
    }
}
