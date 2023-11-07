namespace Minio.FileSystem.Client.Models
{
    public class CreateDirectoryModel
    {
        public long? TenantId { get; set; }
        public string VirtualPath { get; set; }
    }
}
