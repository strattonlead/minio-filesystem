namespace Minio.FileSystem.Client.Models
{
    public class GetSizeModel
    {
        public long? TenantId { get; set; }
        public string VirtualPath { get; set; }
    }
}
