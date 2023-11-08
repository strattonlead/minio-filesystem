using System;

namespace Minio.FileSystem.Client.Models
{
    public class GetModel
    {
        public Guid? Id { get; set; }
        public long? TenantId { get; set; }
        public string VirtualPath { get; set; }
    }
}
