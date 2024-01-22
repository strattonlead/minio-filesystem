using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Minio.FileSystem.Abstraction;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Minio.FileSystem.Backend
{
    [Index(nameof(TenantId))]
    public class ThumbnailEntity
    {
        public Guid Id { get; set; }
        public Guid FileSystemItemId { get; set; }
        public FileSystemItemEntity FileSystemItem { get; set; }
        public ThumbnailType ThumbnailType { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long? SizeInBytes { get; set; }
        public string ContentType { get; set; }
        public long? TenantId { get; set; }

        [NotMapped]
        public string StoragePath => ThumbnailType == ThumbnailType.Image ? $"{Id}.thumb.png" : $"{Id}.thumb.gif";
    }

    public class ThumbnailEntityTypeConfiguration : IEntityTypeConfiguration<ThumbnailEntity>
    {
        public void Configure(EntityTypeBuilder<ThumbnailEntity> builder)
        {
            builder.HasKey(x => x.Id);
        }
    }
}
