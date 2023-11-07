namespace Minio.FileSystem.WebApi.Models
{
    public class FilterModel
    {
        public long? TenantId { get; set; }
        public string VirtualPath { get; set; }
        public string Filter { get; set; }
    }
}
