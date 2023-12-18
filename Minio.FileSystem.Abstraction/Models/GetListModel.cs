using System;

namespace Minio.FileSystem.Models
{
    public class GetListModel
    {
        public Guid? Id { get; set; }
        public string VirtualPath { get; set; }
        public long? TenantId { get; set; }
    }
}
