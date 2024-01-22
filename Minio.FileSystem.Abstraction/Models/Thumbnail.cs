using Minio.FileSystem.Abstraction;
using System;

namespace Minio.FileSystem.Models
{
    public class Thumbnail
    {
        public Guid Id { get; set; }
        public ThumbnailType ThumbnailType { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long? SizeInBytes { get; set; }
        public string ContentType { get; set; }
        public long? TenantId { get; set; }
    }
}
