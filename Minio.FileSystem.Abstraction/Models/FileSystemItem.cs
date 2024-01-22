using Minio.FileSystem.Abstraction;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;

namespace Minio.FileSystem.Models
{
    public class FileSystemItem : IFileSystemItem
    {
        public Guid Id { get; set; }
        public Guid FileSystemId { get; set; }
        public FileSystem FileSystem { get; set; }

        public Guid? ParentId { get; set; }
        public FileSystem Parent { get; set; }

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

        public List<Thumbnail> Thumbnails { get; set; }
    }
}
