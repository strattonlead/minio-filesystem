using System;
using System.Collections.Generic;

namespace Minio.FileSystem.Abstraction
{
    public interface IFileSystemItem
    {
        Guid Id { get; set; }
        Guid FileSystemId { get; set; }
        Guid? ParentId { get; set; }
        string Name { get; set; }
        long? SizeInBytes { get; set; }
        string ContentType { get; set; }
        string ExternalUrl { get; set; }
        string VirtualPath { get; set; }
        long? TenantId { get; set; }
        FileSystemItemType FileSystemItemType { get; set; }
        Dictionary<string, object> MetaProperties { get; set; }
    }
}