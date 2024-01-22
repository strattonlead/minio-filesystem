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

        public bool IsCached(FileSystemItemEntity fileSystemItem)
        {
            var path = Path.Combine("cache", fileSystemItem.StoragePath);
            return File.Exists(path);
        }

        public bool IsCached(ThumbnailEntity thumbnail)
        {
            var path = Path.Combine("cache", thumbnail.StoragePath);
            return File.Exists(path);
        }

        public Stream OpenReadStream(FileSystemItemEntity fileSystemItem)
        {
            var path = Path.Combine("cache", fileSystemItem.StoragePath);
            if (File.Exists(path))
            {
                return File.OpenRead(path);
            }
            return null;
        }

        public Stream OpenReadStream(ThumbnailEntity thumbnail)
        {
            var path = Path.Combine("cache", thumbnail.StoragePath);
            if (File.Exists(path))
            {
                return File.OpenRead(path);
            }
            return null;
        }

        public void Cache(FileSystemItemEntity fileSystemItem, Stream stream)
        {
            var path = Path.Combine("cache", fileSystemItem.StoragePath);
            if (File.Exists(path))
            {
                return;
            }

            using (var file = File.OpenWrite(path))
            {
                stream.CopyTo(file);
            }
        }

        public void Cache(ThumbnailEntity thumbnail, Stream stream)
        {
            var path = Path.Combine("cache", thumbnail.StoragePath);
            if (File.Exists(path))
            {
                return;
            }

            using (var file = File.OpenWrite(path))
            {
                stream.CopyTo(file);
            }
        }

        public Stream OpenWriteStream(FileSystemItemEntity fileSystemItem)
        {
            var path = Path.Combine("cache", fileSystemItem.StoragePath);
            if (File.Exists(path))
            {
                return null;
            }

            return File.OpenWrite(path);
        }

        public Stream OpenWriteStream(ThumbnailEntity thumbnail)
        {
            var path = Path.Combine("cache", thumbnail.StoragePath);
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
