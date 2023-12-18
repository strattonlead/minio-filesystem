using System;

namespace Minio.FileSystem.Models
{
    public class GetModel
    {
        public Guid? Id { get; set; }
        public long? TenantId { get; set; }
        public string VirtualPath { get; set; }
    }
}
