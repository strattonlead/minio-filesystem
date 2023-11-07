namespace Minio.FileSystem.WebApi.Models
{
    public class CreateFileSystemModel
    {
        public long? TenantId { get; set; }
        public string Name { get; set; }
    }
}
