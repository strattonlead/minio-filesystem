using System;

namespace Minio.FileSystem.WebApi.Models
{
    public class DeleteModel
    {
        public long? TenantId { get; set; }
        public Guid? Id { get; set; }
        public string VirtualPath { get; set; }
    }
}
