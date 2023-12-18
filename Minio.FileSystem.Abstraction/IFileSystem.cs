using System;

namespace Minio.FileSystem.Abstraction
{
    public interface IFileSystem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public long? TenantId { get; set; }
    }
}
