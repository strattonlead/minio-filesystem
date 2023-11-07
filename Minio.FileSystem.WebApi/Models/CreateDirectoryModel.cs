namespace Minio.FileSystem.WebApi.Models
{
    public class CreateDirectoryModel
    {
        public long? TenantId { get; set; }
        public string VirtualPath { get; set; }
    }
}
