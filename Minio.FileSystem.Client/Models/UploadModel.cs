using System.IO;

namespace Minio.FileSystem.Client.Models
{
    public class UploadModel
    {
        public Stream Stream { get; set; }
        public string Name { get; set; }
        public string ContentType { get; set; }
        public string VirtualPath { get; set; }
    }
}
