namespace Minio.FileSystem.Models
{
    public class GetSizeModel
    {
        public long? TenantId { get; set; }
        public string VirtualPath { get; set; }
    }
}
