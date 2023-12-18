namespace Minio.FileSystem.Models
{
    public class FilterModel
    {
        public long? TenantId { get; set; }
        public string VirtualPath { get; set; }
        public string Filter { get; set; }
    }
}
