using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;

namespace Minio.FileSystem.Backend
{
    [Index(nameof(TenantId))]
    [Index(nameof(VirtualPath))]
    public class FileSystemItemEntity
    {
        public Guid Id { get; set; }
        public Guid FileSystemId { get; set; }
        public FileSystemEntity FileSystem { get; set; }

        public Guid? ParentId { get; set; }
        public FileSystemItemEntity Parent { get; set; }
        public List<FileSystemItemEntity> Children { get; set; }

        public string Name { get; set; }
        public long? SizeInBytes { get; set; }
        public string ContentType { get; set; }
        public string ExternalUrl { get; set; }
        public string VirtualPath { get; set; }
        public long? TenantId { get; set; }

        public FileSystemItemType FileSystemItemType { get; set; }
        public Dictionary<string, object> MetaProperties { get; set; }

        [NotMapped]
        public string StoragePath => FileSystemItemType == FileSystemItemType.File ? $"{Id}{Path.GetExtension(Name)}" : null;

        [NotMapped]
        public bool IsFile => FileSystemItemType == FileSystemItemType.File;

        [NotMapped]
        public bool IsDirectory => FileSystemItemType == FileSystemItemType.Directory;

        [NotMapped]
        public bool IsExternalLink => FileSystemItemType == FileSystemItemType.ExternalLink;
    }

    public class FileSystemItemEntityTypeConfiguration : IEntityTypeConfiguration<FileSystemItemEntity>
    {
        public void Configure(EntityTypeBuilder<FileSystemItemEntity> builder)
        {
            builder.HasKey(x => x.Id);

            builder.HasMany(x => x.Children)
                .WithOne(x => x.Parent)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.ClientCascade);

            builder.Property(x => x.MetaProperties)
                .HasConversion(x => JsonConvert.SerializeObject(x), x => !string.IsNullOrWhiteSpace(x) ? JsonConvert.DeserializeObject<Dictionary<string, object>>(x) : null);
        }
    }
}
