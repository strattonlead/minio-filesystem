using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Minio.FileSystem.Abstraction;
using System;
using System.Collections.Generic;

namespace Minio.FileSystem.Backend
{
    [Index(nameof(TenantId))]
    public class FileSystemEntity : IFileSystem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public long? TenantId { get; set; }
        public ICollection<FileSystemItemEntity> FileSystemItems { get; set; }
    }

    public class FileSystemEntityTypeConfiguration : IEntityTypeConfiguration<FileSystemEntity>
    {
        public void Configure(EntityTypeBuilder<FileSystemEntity> builder)
        {
            builder.HasKey(x => x.Id);
            builder.HasMany(x => x.FileSystemItems)
                .WithOne(x => x.FileSystem)
                .HasForeignKey(x => x.FileSystemId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}