using Microsoft.Extensions.DependencyInjection;
using Minio.FileSystem.Backend;
using System.IO;

namespace Minio.FileSystem.Services
{
    public class FileCacheService
    {
        public FileCacheService()
        {
            if (!Directory.Exists("cache"))
            {
                Directory.CreateDirectory("cache");
            }
        }

        public bool IsCached(FileSystemItemEntity FileSystemItem)
        {
            var path = Path.Combine("cache", FileSystemItem.StoragePath);
            return File.Exists(path);
        }

        public Stream OpenReadStream(FileSystemItemEntity FileSystemItem)
        {
            var path = Path.Combine("cache", FileSystemItem.StoragePath);
            if (File.Exists(path))
            {
                return File.OpenRead(path);
            }
            return null;
        }

        public void Cache(FileSystemItemEntity FileSystemItem, Stream stream)
        {
            var path = Path.Combine("cache", FileSystemItem.StoragePath);
            if (File.Exists(path))
            {
                return;
            }

            using (var file = File.OpenWrite(path))
            {
                stream.CopyTo(file);
            }
        }

        public Stream OpenWriteStream(FileSystemItemEntity FileSystemItem)
        {
            var path = Path.Combine("cache", FileSystemItem.StoragePath);
            if (File.Exists(path))
            {
                return null;
            }

            return File.OpenWrite(path);
        }
    }

    public static class FileCacheServiceExtensions
    {
        public static void AddFileCacheService(this IServiceCollection services)
        {
            services.AddSingleton<FileCacheService>();
        }
    }
}
