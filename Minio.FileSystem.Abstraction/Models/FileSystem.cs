using Minio.FileSystem.Abstraction;
using System;
using System.Collections.Generic;

namespace Minio.FileSystem.Models
{
    public class FileSystem : IFileSystem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public long? TenantId { get; set; }
        public ICollection<FileSystemItem> FileSystemItems { get; set; }
    }
}
