namespace Minio.FileSystem.Models
{
    public class CreateLinkModel
    {
        public long? TenantId { get; set; }
        public string VirtualPath { get; set; }
        public string Url { get; set; }
    }
}
