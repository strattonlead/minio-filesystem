using System.IO;

namespace Minio.FileSystem.Models
{
    public class UploadModel
    {
        public long? TenantId { get; set; }
        public Stream Stream { get; set; }
        public string Name { get; set; }
        public string ContentType { get; set; }
        public string VirtualPath { get; set; }
    }
}
