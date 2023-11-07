using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Minio.FileSystem.Services
{
    public class DataProtectionDbContext : DbContext, IDataProtectionKeyContext
    {
        public DataProtectionDbContext(DbContextOptions<DataProtectionDbContext> dbContextOptions)
        : base(dbContextOptions) { }
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
    }
}
