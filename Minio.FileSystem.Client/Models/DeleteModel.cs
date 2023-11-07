namespace Minio.FileSystem.Client.Models
{
    public class DeleteModel
    {
        public long? TenantId { get; set; }
        public string VirtualPath { get; set; }
    }
}
