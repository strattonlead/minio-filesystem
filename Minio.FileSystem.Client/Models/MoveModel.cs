namespace Minio.FileSystem.Client.Models
{
    public class MoveModel
    {
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public bool Override { get; set; }
    }
}
