using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Minio.Filesystem.Backend
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(IServiceProvider serviceProvider)
                : base(serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>()) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }

        public DbSet<FileSystemEntity> FileSystems { get; set; }
        public DbSet<FileSystemItemEntity> FileSystemItems { get; set; }
    }
}
