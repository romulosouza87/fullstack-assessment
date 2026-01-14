using Microsoft.EntityFrameworkCore;
using CryptoPriceTracker.Api.Models;

namespace CryptoPriceTracker.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<CryptoAsset> CryptoAssets => Set<CryptoAsset>();
        public DbSet<CryptoPriceHistory> CryptoPriceHistories => Set<CryptoPriceHistory>();
    }
}
