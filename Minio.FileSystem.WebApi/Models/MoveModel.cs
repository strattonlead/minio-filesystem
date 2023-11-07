namespace Minio.FileSystem.WebApi.Models
{
    public class MoveModel
    {
        public long? TenantId { get; set; }
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public bool Override { get; set; }
    }
}
