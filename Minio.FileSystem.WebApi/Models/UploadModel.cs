using Microsoft.AspNetCore.Http;

namespace Minio.FileSystem.WebApi.Models
{
    public class UploadModel
    {
        public IFormFile File { get; set; }
        public string VirtualPath { get; set; }
    }
}
