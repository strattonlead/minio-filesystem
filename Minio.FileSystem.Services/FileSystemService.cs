using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Minio.Filesystem.Backend;
using Muffin.Tenancy.Services.Abstraction;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace Minio.FileSystem.Services
{
    public class FileSystemService
    {
        #region Properties

        private readonly ApplicationDbContext _dbContext;
        private readonly IMinioClient _minioClient;
        private readonly ITenantProvider _tenantProvider;
        private readonly ApplicationOptions _options;
        private long? _tenantId => _options.TenancyEnabled ? _tenantProvider.ActiveTenant?.Id : null;

        #endregion

        #region Constructor

        public FileSystemService(IServiceProvider serviceProvider)
        {
            _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            _minioClient = serviceProvider.GetRequiredService<IMinioClient>();
            _tenantProvider = serviceProvider.GetRequiredService<ITenantProvider>();
            _options = serviceProvider.GetRequiredService<ApplicationOptions>();
        }

        #endregion

        #region FileSystem

        public async Task<FileSystemEntity> CreateFileSystemAsync(string name, CancellationToken cancellationToken = default)
        {
            var fileSystem = new FileSystemEntity()
            {
                Name = name,
                TenantId = _tenantId
            };

            _dbContext.Add(fileSystem);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return fileSystem;
        }

        public async Task<FileSystemEntity> RenameFileSystemAsync(Guid id, string name, CancellationToken cancellationToken = default)
        {
            var fileSystem = await _dbContext.FileSystems.FindAsync(id);
            if (fileSystem == null)
            {
                return null;
            }

            fileSystem.Name = name;

            _dbContext.Update(fileSystem);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return fileSystem;
        }

        public async Task<Guid?> DeleteFileSystemAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fileSystem = await _dbContext.FileSystems.FindAsync(id);
            if (fileSystem == null)
            {
                return null;
            }

            if (fileSystem.FileSystemItems == null)
            {
                await _dbContext.Entry(fileSystem).Collection(x => x.FileSystemItems).LoadAsync(cancellationToken);
            }

            foreach (var fileSystemItem in fileSystem.FileSystemItems)
            {
                if (fileSystemItem.IsFile)
                {
                    await _minioClient.RemoveObjectAsync(fileSystemItem, cancellationToken);
                }
                _dbContext.Remove(fileSystemItem);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _dbContext.Remove(fileSystem);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return fileSystem.Id;
        }

        #endregion

        #region FileSystemItem

        public async Task<FileSystemItemEntity> FindAsync(FileSystemPath path, CancellationToken cancellationToken = default)
        {
            return await _dbContext.FileSystemItems.FirstOrDefaultAsync(x => x.FileSystemId == path.FileSystemId && x.VirtualPath == path.VirtualPath, cancellationToken);
        }

        public async Task CopyToStreamAsync(FileSystemItemEntity fileSystemItem, Stream output)
        {
            await _minioClient.GetObjectAsync(fileSystemItem, output);
        }

        public async Task<FileSystemItemEntity> UploadAsync(FileSystemPath path, IFormFile file, CancellationToken cancellationToken = default)
        {
            if (!path.IsValid)
            {
                return null;
            }

            var fileSystemItem = await FindAsync(path, cancellationToken);
            if (fileSystemItem == null)
            {
                return await _addAsync(path, file, cancellationToken);
            }

            return await _updateAsync(fileSystemItem, path, file, cancellationToken);
        }

        public async Task<FileSystemItemEntity[]> FilterAsync(string filter, CancellationToken cancellationToken)
        {
            var query = (IQueryable<FileSystemItemEntity>)_dbContext.FileSystemItems;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(x => x.Name.Contains(filter));
            }

            if (_options.TenancyEnabled)
            {
                var tenantId = _tenantProvider.ActiveTenant?.Id;
                query = query.Where(x => !x.TenantId.HasValue || x.TenantId == tenantId);
            }

            return await query.ToArrayAsync(cancellationToken);
        }

        public async Task<FileSystemEntity[]> FilterFileSystemsAsync(string filter, CancellationToken cancellationToken = default)
        {
            var query = (IQueryable<FileSystemEntity>)_dbContext.FileSystems;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(x => x.Name.Contains(filter));
            }

            if (_options.TenancyEnabled)
            {
                var tenantId = _tenantProvider.ActiveTenant?.Id;
                query = query.Where(x => !x.TenantId.HasValue || x.TenantId == tenantId);
            }

            return await query.ToArrayAsync(cancellationToken);
        }

        public async Task<FileSystemItemEntity> CreateDirectoryAsync(FileSystemPath path, CancellationToken cancellationToken = default)
        {
            var fileSystemItem = await FindAsync(path, cancellationToken);
            if (fileSystemItem == null)
            {
                fileSystemItem = new FileSystemItemEntity()
                {
                    FileSystemItemType = FileSystemItemType.Directory,
                    Name = Path.GetFileName(path.VirtualPath),
                    VirtualPath = path.VirtualPath,
                    TenantId = _tenantId
                };

                _dbContext.Add(fileSystemItem);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return fileSystemItem;
        }

        public async Task<Guid?> DeleteAsync(FileSystemPath path, CancellationToken cancellationToken = default)
        {
            var fileSystemItem = await FindAsync(path, cancellationToken);
            if (fileSystemItem == null)
            {
                return null;
            }

            if (fileSystemItem.IsFile)
            {
                await _minioClient.RemoveObjectAsync(fileSystemItem, cancellationToken);
            }
            else if (fileSystemItem.IsDirectory)
            {
                var children = await _children(fileSystemItem, cancellationToken);
                foreach (var child in children)
                {
                    if (child.IsFile)
                    {
                        await _minioClient.RemoveObjectAsync(child, cancellationToken);
                    }
                    _dbContext.Remove(child);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            _dbContext.Remove(fileSystemItem);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return fileSystemItem.Id;
        }

        public async Task<FileSystemItemEntity> MoveAsync(FileSystemPath source, FileSystemPath destination, bool @override, CancellationToken cancellationToken = default)
        {
            if (source == destination)
            {
                return null;
            }

            var sourceFileSystemItem = await FindAsync(source, cancellationToken);
            if (sourceFileSystemItem == null)
            {
                return null;
            }

            /*
             * Nur File -> File, Directory -> Directory und Link -> Link ist erlaubt
             */
            var destinationFileSystemItem = await FindAsync(destination, cancellationToken);
            if (destinationFileSystemItem != null)
            {
                if (sourceFileSystemItem.IsDirectory != destinationFileSystemItem.IsDirectory)
                {
                    return null;
                }

                if (sourceFileSystemItem.IsFile != destinationFileSystemItem.IsFile)
                {
                    return null;
                }

                if (sourceFileSystemItem.IsExternalLink != destinationFileSystemItem.IsExternalLink)
                {
                    return null;
                }
            }

            if (destinationFileSystemItem != null && !@override)
            {
                return null;
            }

            if (sourceFileSystemItem.IsFile)
            {
                if (destinationFileSystemItem != null)
                {
                    await DeleteAsync(destination, cancellationToken);
                }

                sourceFileSystemItem.Name = Path.GetFileName(destination.VirtualPath);
                sourceFileSystemItem.VirtualPath = destination.VirtualPath;
                sourceFileSystemItem.FileSystemId = destination.FileSystemId;
            }
            else if (sourceFileSystemItem.IsDirectory)
            {
                if (destinationFileSystemItem != null)
                {
                    await DeleteAsync(destination, cancellationToken);
                }

                sourceFileSystemItem.Name = Path.GetFileName(destination.VirtualPath);
                sourceFileSystemItem.VirtualPath = destination.VirtualPath;
                sourceFileSystemItem.FileSystemId = destination.FileSystemId;

                var children = await _children(sourceFileSystemItem, cancellationToken);
                foreach (var child in children)
                {
                    child.VirtualPath = child.VirtualPath.Replace(source.VirtualPath, destination.VirtualPath);
                    child.FileSystemId = destination.FileSystemId;
                }
            }
            else if (sourceFileSystemItem.IsExternalLink)
            {
                if (destinationFileSystemItem == null)
                {
                    return null;
                }

                sourceFileSystemItem.ExternalUrl = destinationFileSystemItem.ExternalUrl;
                await DeleteAsync(destination, cancellationToken);
            }

            _dbContext.Update(sourceFileSystemItem);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return sourceFileSystemItem;
        }

        #endregion

        #region Helpers

        private async Task<FileSystemItemEntity[]> _children(FileSystemItemEntity fileSystemItem, CancellationToken cancellationToken = default)
        {
            return await _dbContext.FileSystemItems.Where(x => x.VirtualPath.StartsWith(fileSystemItem.VirtualPath)).ToArrayAsync(cancellationToken);
        }

        private async Task<FileSystemItemEntity> _addAsync(FileSystemPath path, IFormFile file, CancellationToken cancellationToken = default)
        {
            var fileSystemItem = new FileSystemItemEntity()
            {
                FileSystemId = path.FileSystemId,
                ContentType = file.ContentType,
                VirtualPath = path.VirtualPath,
                Name = Path.GetFileName(path.VirtualPath),
                FileSystemItemType = FileSystemItemType.File,
                TenantId = _tenantId
            };

            var localPath = Path.GetTempFileName();

            using (var fileStream = File.OpenWrite(localPath))
            {
                file.OpenReadStream().CopyTo(fileStream);
                fileStream.Close();
            }

            using (var fileStream = File.OpenRead(localPath))
            {
                await _readMetaProperties(fileSystemItem, localPath, cancellationToken);
                await _minioClient.PutObjectAsync(fileSystemItem, fileStream, true, cancellationToken);

                fileSystemItem.SizeInBytes = fileStream.Length;

                fileStream.Close();
            }

            if (File.Exists(localPath))
            {
                try
                {
                    File.Delete(localPath);
                }
                catch { }
            }

            _dbContext.Add(fileSystemItem);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return fileSystemItem;
        }

        private async Task<FileSystemItemEntity> _updateAsync(FileSystemItemEntity fileSystemItem, FileSystemPath path, IFormFile file, CancellationToken cancellationToken = default)
        {
            fileSystemItem.ContentType = file.ContentType;
            fileSystemItem.VirtualPath = path.VirtualPath;
            fileSystemItem.Name = Path.GetFileName(path.VirtualPath);
            fileSystemItem.FileSystemItemType = FileSystemItemType.File;
            fileSystemItem.TenantId = _tenantId;

            var localPath = Path.GetTempFileName();

            using (var fileStream = File.OpenWrite(localPath))
            {
                file.OpenReadStream().CopyTo(fileStream);
                fileStream.Close();
            }

            using (var fileStream = File.OpenRead(localPath))
            {
                await _readMetaProperties(fileSystemItem, localPath, cancellationToken);
                await _minioClient.PutObjectAsync(fileSystemItem, fileStream, true, cancellationToken);

                fileSystemItem.SizeInBytes = fileStream.Length;

                fileStream.Close();
            }

            if (File.Exists(localPath))
            {
                try
                {
                    File.Delete(localPath);
                }
                catch { }
            }

            _dbContext.Update(fileSystemItem);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return fileSystemItem;
        }

#pragma warning disable CA1416
        private async Task _readMetaProperties(FileSystemItemEntity fileSystemItem, string localFilePath, CancellationToken cancellationToken = default)
        {
            if (fileSystemItem?.ContentType?.StartsWith("video") ?? false)
            {
                var mediaInfo = await FFmpeg.GetMediaInfo(localFilePath, cancellationToken);
                var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
                if (videoStream != null)
                {
                    fileSystemItem.MetaProperties["videoDuration"] = videoStream.Duration;
                    fileSystemItem.MetaProperties["videoCodec"] = videoStream.Codec;
                    fileSystemItem.MetaProperties["videoBitrate"] = videoStream.Bitrate;
                    fileSystemItem.MetaProperties["videoFramerate"] = videoStream.Framerate;
                    fileSystemItem.MetaProperties["videoPixelFormat"] = videoStream.PixelFormat;
                    fileSystemItem.MetaProperties["videoRatio"] = videoStream.Ratio;
                    fileSystemItem.MetaProperties["videoRotation"] = videoStream.Rotation;
                    fileSystemItem.MetaProperties["videoWidth"] = videoStream.Width;
                    fileSystemItem.MetaProperties["videoHeight"] = videoStream.Height;
                }
            }
            else if (fileSystemItem?.ContentType?.StartsWith("image") ?? false)
            {
                using (var img = Image.FromFile(localFilePath))
                {
                    fileSystemItem.MetaProperties["imageType"] = img.RawFormat.ToString();
                    fileSystemItem.MetaProperties["imageWidth"] = img.Width;
                    fileSystemItem.MetaProperties["imageHeight"] = img.Height;
                    fileSystemItem.MetaProperties["imageResolution"] = img.VerticalResolution * img.HorizontalResolution;
                    fileSystemItem.MetaProperties["imagePixelDepth"] = Image.GetPixelFormatSize(img.PixelFormat);
                }
            }
        }
#pragma warning restore CA1416

        #endregion
    }

    public struct FileSystemPath
    {
        public Guid FileSystemId { get; set; }
        public string VirtualPath { get; set; }
        public bool IsValid { get; set; }

        public static bool operator ==(FileSystemPath path, FileSystemPath other)
        {
            return path.VirtualPath == other.VirtualPath;
        }

        public static bool operator !=(FileSystemPath path, FileSystemPath other)
        {
            return path.VirtualPath != other.VirtualPath;
        }

        public override bool Equals([NotNullWhen(true)] object obj)
        {
            if (obj is not FileSystemPath)
            {
                return false;
            }

            return VirtualPath == ((FileSystemPath)obj).VirtualPath;
        }

        public override int GetHashCode() => VirtualPath?.GetHashCode() ?? 0;

        public static implicit operator FileSystemPath(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return new FileSystemPath() { IsValid = false };
            }

            if (!s.StartsWith("/"))
            {
                return new FileSystemPath() { IsValid = false };
            }

            var parts = s.Split("/");
            if (parts.Length < 2)
            {
                return new FileSystemPath() { IsValid = false };
            }

            if (!Guid.TryParse(parts[1], out var fileSystemId))
            {
                return new FileSystemPath() { IsValid = false };
            }

            return new FileSystemPath()
            {
                FileSystemId = fileSystemId,
                VirtualPath = $"{s}",
                IsValid = true
            };
        }
    }

    public static class FileSystemServiceExtensions
    {
        public static void AddFileSystemService(this IServiceCollection services)
        {
            services.AddScoped<FileSystemService>();
        }
    }
}