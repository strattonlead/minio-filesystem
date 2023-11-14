using System;

namespace Minio.FileSystem.Client.Models
{
    public class CreateZipModel
    {
        public Guid[] Ids { get; set; }
        public string[] VirtualPaths { get; set; }
        public string VirtualPath { get; set; }
        public long? TenantId { get; set; }
    }
}
