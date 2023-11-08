using System;

namespace Minio.FileSystem.Client.Models
{
    public class DeleteModel
    {
        public Guid? Id { get; set; }
        public long? TenantId { get; set; }
        public string VirtualPath { get; set; }
    }
}
