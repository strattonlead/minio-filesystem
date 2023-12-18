namespace Minio.FileSystem.Models
{
    public class CreateDirectoryModel
    {
        public long? TenantId { get; set; }
        public string VirtualPath { get; set; }
    }
}
