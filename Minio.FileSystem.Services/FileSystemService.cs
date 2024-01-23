using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minio.FileSystem.Abstraction;
using Minio.FileSystem.Backend;
using Muffin.Tenancy.Services.Abstraction;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.IO.Compression;
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
        private readonly ApplicationOptions _options;
        private readonly ITenantProvider _tenantProvider;
        private readonly ILogger _logger;

        #endregion

        #region Constructor

        public FileSystemService(IServiceProvider serviceProvider)
        {
            _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            _minioClient = serviceProvider.GetRequiredService<IMinioClient>();
            _tenantProvider = serviceProvider.GetRequiredService<ITenantProvider>();
            _options = serviceProvider.GetRequiredService<ApplicationOptions>();
            _logger = serviceProvider.GetRequiredService<ILogger<FileSystemService>>();
        }

        #endregion

        #region FileSystem

        public async Task<FileSystemEntity> CreateFileSystemAsync(string name, long? tenantId, CancellationToken cancellationToken = default)
        {
            var fileSystem = new FileSystemEntity()
            {
                Name = name,
                TenantId = tenantId
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

            foreach (var FileSystemItem in fileSystem.FileSystemItems)
            {
                if (FileSystemItem.IsFile)
                {
                    _tenantProvider.SetTenant(fileSystem.TenantId);

                    await _minioClient.RemoveObjectAsync(FileSystemItem, cancellationToken);

                    _tenantProvider.RestoreTenancy();
                }
                _dbContext.Remove(FileSystemItem);
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
            return await _dbContext.FileSystemItems.FirstOrDefaultAsync(x => x.FileSystemId == path.FileSystemId && x.VirtualPath == path.VirtualPath && x.TenantId == path.TenantId, cancellationToken);
        }

        public async Task<FileSystemItemEntity> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.FileSystemItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public async Task<ThumbnailEntity> GetThumbnailAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Thumbnails.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public async Task<FileSystemItemEntity[]> GetManyAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            return await _dbContext.FileSystemItems.Where(x => ids.Contains(x.Id)).AsSplitQuery().ToArrayAsync(cancellationToken);
        }

        public async Task<Guid[]> GetIdsAsync(IEnumerable<string> virtualPaths, long? tenantId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.FileSystemItems.Where(x => x.TenantId == tenantId && virtualPaths.Contains(x.VirtualPath)).Select(x => x.Id).ToArrayAsync(cancellationToken);
        }

        public async Task<FileSystemItemEntity[]> GetListAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var item = await GetAsync(id, cancellationToken);
            if (!item.IsDirectory)
            {
                return null;
            }
            return await _children(item, cancellationToken);
        }

        public async Task<FileSystemItemEntity[]> GetListAsync(FileSystemPath path, CancellationToken cancellationToken = default)
        {
            if (path.IsRoot)
            {
                return await _dbContext.FileSystemItems.Where(x => x.FileSystemId == path.FileSystemId && !x.ParentId.HasValue).AsSplitQuery().ToArrayAsync(cancellationToken);
            }

            var item = await FindAsync(path, cancellationToken);
            if (item == null)
            {
                return null;
            }

            if (!item.IsDirectory)
            {
                return null;
            }
            return await _children(item, cancellationToken);
        }

        public async Task<bool> CopyToStreamAsync(FileSystemItemEntity fileSystemItem, Stream output, CancellationToken cancellationToken)
        {
            _tenantProvider.SetTenant(fileSystemItem.TenantId);
            try
            {
                await _minioClient.GetObjectAsync(fileSystemItem, output, cancellationToken);
                return true;
            }
            catch { }
            finally
            {
                _tenantProvider.RestoreTenancy();
            }
            return false;
        }

        public async Task<bool> CopyToStreamAsync(ThumbnailEntity thumbnail, Stream output, CancellationToken cancellationToken)
        {
            _tenantProvider.SetTenant(thumbnail.TenantId);
            try
            {
                await _minioClient.GetObjectAsync(thumbnail, output, cancellationToken);
                return true;
            }
            catch { }
            finally
            {
                _tenantProvider.RestoreTenancy();
            }
            return false;
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

        public async Task<FileSystemEntity[]> GetFileSystemsAsync(long? tenantId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.FileSystems.Where(x => x.TenantId == tenantId).ToArrayAsync(cancellationToken);
        }

        public async Task<FileSystemEntity[]> GetAllFileSystemsAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.FileSystems.ToArrayAsync(cancellationToken);
        }

        public async Task<long> GetSizeAsync(FileSystemPath path, CancellationToken cancellationToken = default)
        {
            if (path.IsRoot)
            {
                return await _dbContext.FileSystemItems.Where(x => x.FileSystemId == path.FileSystemId).IgnoreAutoIncludes().SumAsync(x => x.SizeInBytes) ?? 0;
            }

            return await _dbContext.FileSystemItems.Where(x => x.VirtualPath.Contains(path.VirtualPath)).IgnoreAutoIncludes().SumAsync(x => x.SizeInBytes) ?? 0;
        }

        public async Task<FileSystemItemEntity> CreateLinkAsync(FileSystemPath path, string url, CancellationToken cancellationToken = default)
        {
            var fileSystemItem = await FindAsync(path, cancellationToken);
            if (fileSystemItem != null)
            {
                return null;
            }

            fileSystemItem = new FileSystemItemEntity()
            {
                FileSystemId = path.FileSystemId,
                ContentType = "text/uri-list",
                VirtualPath = path.VirtualPath,
                Name = Path.GetFileName(path.VirtualPath),
                FileSystemItemType = FileSystemItemType.ExternalLink,
                TenantId = path.TenantId,
                ExternalUrl = url
            };
            _dbContext.Add(fileSystemItem);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return fileSystemItem;
        }

        public async Task<FileSystemItemEntity[]> FilterAsync(string filter, string virtualPath, long? tenantId, CancellationToken cancellationToken)
        {
            var query = _dbContext.FileSystemItems.Where(x => x.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(x => x.Name.Contains(filter));
            }

            if (!string.IsNullOrWhiteSpace(virtualPath))
            {
                query = query.Where(x => x.VirtualPath.StartsWith(virtualPath));
            }

            return await query.ToArrayAsync(cancellationToken);
        }

        public async Task<FileSystemEntity[]> FilterFileSystemsAsync(string filter, long? tenantId, CancellationToken cancellationToken = default)
        {
            var query = _dbContext.FileSystems.Where(x => x.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(x => x.Name.Contains(filter));
            }

            return await query.ToArrayAsync(cancellationToken);
        }

        public async Task<FileSystemItemEntity> CreateDirectoryAsync(FileSystemPath path, CancellationToken cancellationToken = default)
        {
            return await _addDirectoryAsync(path, cancellationToken);
        }

        public async Task<Guid?> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var fileSystemItem = await _dbContext.FileSystemItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (fileSystemItem == null)
            {
                return null;
            }

            return await _deleteAsync(fileSystemItem, cancellationToken);
        }

        public async Task<Guid?> DeleteAsync(FileSystemPath path, CancellationToken cancellationToken = default)
        {
            var fileSystemItem = await FindAsync(path, cancellationToken);
            if (fileSystemItem == null)
            {
                return null;
            }

            return await _deleteAsync(fileSystemItem, cancellationToken);
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

            if (sourceFileSystemItem.IsFile || sourceFileSystemItem.IsExternalLink)
            {
                if (destinationFileSystemItem != null)
                {
                    await DeleteAsync(destination, cancellationToken);
                }

                sourceFileSystemItem.Name = Path.GetFileName(destination.VirtualPath);
                sourceFileSystemItem.VirtualPath = destination.VirtualPath;
                sourceFileSystemItem.FileSystemId = destination.FileSystemId;
                sourceFileSystemItem.ParentId = destinationFileSystemItem.ParentId;

                _dbContext.Update(sourceFileSystemItem);
                await _dbContext.SaveChangesAsync(cancellationToken);
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
                sourceFileSystemItem.ParentId = destinationFileSystemItem.ParentId;

                _dbContext.Update(sourceFileSystemItem);
                await _dbContext.SaveChangesAsync(cancellationToken);

                var children = await _allChildren(sourceFileSystemItem, cancellationToken);
                foreach (var child in children)
                {
                    var childItem = await FindAsync(destination, cancellationToken);
                    child.VirtualPath = child.VirtualPath.Replace(source.VirtualPath, destination.VirtualPath);
                    child.FileSystemId = destination.FileSystemId;
                    child.ParentId = childItem.ParentId;

                    _dbContext.Update(child);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            return sourceFileSystemItem;
        }

        public async Task ZipToStreamAsync(FileSystemItemEntity fileSystemItem, Stream stream, CancellationToken cancellationToken = default)
        {
            await _zipAsync(new FileSystemItemEntity[] { fileSystemItem }, stream, cancellationToken);
        }

        public async Task<FileSystemItemEntity> CreateZipAsync(IEnumerable<Guid> ids, /*FileSystemPath path,*/ CancellationToken cancellationToken = default)
        {
            var fileSystemItems = await GetManyAsync(ids, cancellationToken);
            if (fileSystemItems == null || !fileSystemItems.Any())
            {
                return null;
            }
            //var fileSystemItem = await FindAsync(path, cancellationToken);

            var localPath = Path.GetTempFileName();

            try
            {
                using (var stream = File.OpenWrite(localPath))
                {
                    await _zipAsync(fileSystemItems, stream, cancellationToken);
                }

                using (var stream = File.OpenRead(localPath))
                {
                    var fileSystemItem = fileSystemItems.FirstOrDefault();
                    var path = FileSystemPath.FromString($"{fileSystemItem.VirtualPath}.zip", fileSystemItem.TenantId);
                    return await _addAsync(path, "application/zip", stream, cancellationToken);

                    //return await _updateAsync(fileSystemItem, path, "application/zip", stream, cancellationToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Unable to create Zip");
            }
            finally
            {
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }
            }

            return null;
        }

        public async Task<bool> UnzipAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var item = await GetAsync(id, cancellationToken);
            if (item == null)
            {
                return false;
            }

            if (item.ContentType != "application/zip")
            {
                return false;
            }

            var localPath = Path.GetTempFileName();
            try
            {
                using (var stream = File.OpenWrite(localPath))
                {
                    var success = await CopyToStreamAsync(item, stream, cancellationToken);
                    if (!success)
                    {
                        return false;
                    }
                }

                using (var stream = File.OpenRead(localPath))
                using (var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read, true))
                {
                    foreach (var entry in zipArchive.Entries)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return false;
                        }

                        if (entry.Name.EndsWith("/"))
                        {
                            continue;
                        }

                        using (var entryStream = entry.Open())
                        {
                            var path = FileSystemPath.FromString(entry.FullName, item.TenantId);
                            var contentType = _getMimeType(entry.Name);
                            await _addAsync(path, contentType, entryStream, cancellationToken);
                        }
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Unable to unzip {id}");
            }
            finally
            {
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }
            }
            return false;
        }

        #endregion

        #region Helpers

        private string _getMimeType(string fileName)
        {
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(fileName, out var contentType))
            {
                contentType = "application/octet-stream";
            }
            return contentType;
        }

        private async Task _zipAsync(IEnumerable<FileSystemItemEntity> fileSystemItems, Stream stream, CancellationToken cancellationToken = default)
        {
            using (var zipArchive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                foreach (FileSystemItemEntity fileSystemItem in fileSystemItems)
                {
                    if (fileSystemItem.IsExternalLink)
                    {
                        continue;
                    }

                    FileSystemItemEntity[] items;
                    if (fileSystemItem.IsDirectory)
                    {
                        items = await _allChildren(fileSystemItem, cancellationToken);
                    }
                    else
                    {
                        items = new FileSystemItemEntity[] { fileSystemItem };
                    }

                    foreach (var item in items)
                    {
                        if (item.IsDirectory)
                        {
                            continue;
                        }

                        var path = string.Join("/", item.VirtualPath.Split("/").Skip(2));
                        var entry = zipArchive.CreateEntry(path, CompressionLevel.Optimal);
                        using (var entryStream = entry.Open())
                        {
                            await CopyToStreamAsync(item, entryStream, cancellationToken);
                        }
                    }
                }
            }
        }

        private async Task<FileSystemItemEntity> _parent(FileSystemItemEntity fileSystemItem, CancellationToken cancellationToken = default)
        {
            if (fileSystemItem == null)
            {
                return null;
            }

            if (fileSystemItem.Parent == null && fileSystemItem.ParentId.HasValue)
            {
                await _dbContext.Entry(fileSystemItem).Reference(x => x.Parent).LoadAsync(cancellationToken);
            }

            return fileSystemItem.Parent;
        }

        private async Task<FileSystemItemEntity> _parent(Guid id, CancellationToken cancellationToken = default)
        {
            var fileSystemItem = await GetAsync(id, cancellationToken);
            return await _parent(fileSystemItem, cancellationToken);
        }

        private async Task<FileSystemItemEntity> _parent(FileSystemPath path, CancellationToken cancellationToken = default)
        {
            var fileSystemItem = await FindAsync(path, cancellationToken);
            return await _parent(fileSystemItem, cancellationToken);
        }

        private async Task<FileSystemItemEntity[]> _children(FileSystemItemEntity fileSystemItem, CancellationToken cancellationToken = default)
        {
            return await _dbContext.FileSystemItems.Where(x => x.ParentId == fileSystemItem.Id).ToArrayAsync(cancellationToken);
        }

        private async Task<FileSystemItemEntity[]> _allChildren(FileSystemItemEntity fileSystemItem, CancellationToken cancellationToken = default)
        {
            return await _dbContext.FileSystemItems.Where(x => x.VirtualPath.StartsWith(fileSystemItem.VirtualPath)).ToArrayAsync(cancellationToken);
        }

        private async Task<FileSystemItemEntity> _addDirectoryAsync(FileSystemPath path, CancellationToken cancellationToken = default)
        {
            if (path.IsRoot)
            {
                return null;
            }

            var fileSystemItem = await FindAsync(path, cancellationToken);
            if (fileSystemItem != null && fileSystemItem.IsDirectory)
            {
                return fileSystemItem;
            }

            var parts = path.VirtualPath.Split("/").Skip(1).ToArray();
            var tempPath = $"/{parts.FirstOrDefault()}";
            var parentPath = FileSystemPath.FromString(tempPath, path.TenantId);
            var parent = await FindAsync(parentPath, cancellationToken);
            foreach (var part in parts.Skip(1))
            {
                tempPath = $"{tempPath}/{part}";
                var fileSystemPath = FileSystemPath.FromString(tempPath, path.TenantId);
                fileSystemItem = await FindAsync(fileSystemPath, cancellationToken);

                if (fileSystemItem == null)
                {
                    fileSystemItem = new FileSystemItemEntity()
                    {
                        FileSystemId = path.FileSystemId,
                        FileSystemItemType = FileSystemItemType.Directory,
                        Name = Path.GetFileName(tempPath),
                        VirtualPath = tempPath,
                        TenantId = path.TenantId,
                        ParentId = parent?.Id
                    };

                    _dbContext.Add(fileSystemItem);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                parent = fileSystemItem;
            }

            return fileSystemItem;
        }

        private async Task<FileSystemItemEntity> _addAsync(FileSystemPath path, IFormFile file, CancellationToken cancellationToken = default)
        {
            return await _addAsync(path, file.ContentType, file.OpenReadStream(), cancellationToken);
        }

        private string _parentPath(string path)
        {
            var parts = path.Split("/");
            return string.Join("/", parts.Take(parts.Length - 1));
        }

        private FileSystemPath _parentPath(FileSystemPath path)
        {
            return FileSystemPath.FromString(_parentPath(path.VirtualPath), path.TenantId);
        }

        private async Task<FileSystemItemEntity> _addAsync(FileSystemPath path, string contentType, Stream stream, CancellationToken cancellationToken = default)
        {
            var parentPath = _parentPath(path);
            var parent = await _addDirectoryAsync(parentPath, cancellationToken);

            var fileSystemItem = new FileSystemItemEntity()
            {
                FileSystemId = path.FileSystemId,
                ContentType = contentType,
                VirtualPath = path.VirtualPath,
                Name = Path.GetFileName(path.VirtualPath),
                FileSystemItemType = FileSystemItemType.File,
                TenantId = path.TenantId,
                Parent = parent
            };

            _dbContext.Add(fileSystemItem);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var localPath = Path.GetTempFileName();

            using (var fileStream = File.OpenWrite(localPath))
            {
                stream.CopyTo(fileStream);
                fileStream.Close();
            }

            using (var fileStream = File.OpenRead(localPath))
            {
                _tenantProvider.SetTenant(path.TenantId);

                await _readMetaProperties(fileSystemItem, localPath, cancellationToken);
                await _minioClient.PutObjectAsync(fileSystemItem, fileStream, true, cancellationToken);

                _tenantProvider.RestoreTenancy();

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

        private async Task<FileSystemItemEntity> _updateAsync(FileSystemItemEntity fileSystemItem, FileSystemPath path, IFormFile file, CancellationToken cancellationToken = default)
        {
            return await _updateAsync(fileSystemItem, path, file.ContentType, file.OpenReadStream(), cancellationToken);
        }

        private async Task<FileSystemItemEntity> _updateAsync(FileSystemItemEntity fileSystemItem, FileSystemPath path, string contentType, Stream stream, CancellationToken cancellationToken = default)
        {
            fileSystemItem.ContentType = contentType;
            fileSystemItem.VirtualPath = path.VirtualPath;
            fileSystemItem.Name = Path.GetFileName(path.VirtualPath);
            fileSystemItem.FileSystemItemType = FileSystemItemType.File;
            fileSystemItem.TenantId = path.TenantId;

            var localPath = Path.GetTempFileName();

            using (var fileStream = File.OpenWrite(localPath))
            {
                stream.CopyTo(fileStream);
                fileStream.Close();
            }

            using (var fileStream = File.OpenRead(localPath))
            {
                _tenantProvider.SetTenant(path.TenantId);

                await _readMetaProperties(fileSystemItem, localPath, cancellationToken);
                await _minioClient.PutObjectAsync(fileSystemItem, fileStream, true, cancellationToken);

                _tenantProvider.RestoreTenancy();


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

        private async Task<Guid?> _deleteAsync(FileSystemItemEntity fileSystemItem, CancellationToken cancellationToken = default)
        {
            _tenantProvider.SetTenant(fileSystemItem.TenantId);
            if (fileSystemItem.IsFile)
            {
                await _minioClient.RemoveObjectAsync(fileSystemItem, cancellationToken);
            }
            else if (fileSystemItem.IsDirectory)
            {
                var children = await _allChildren(fileSystemItem, cancellationToken);
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
            _tenantProvider.RestoreTenancy();

            _dbContext.Remove(fileSystemItem);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return fileSystemItem.Id;
        }

        private async Task _readMetaProperties(FileSystemItemEntity fileSystemItem, string localFilePath, CancellationToken cancellationToken = default)
        {
            if (fileSystemItem?.ContentType?.StartsWith("video") ?? false)
            {
                var mediaInfo = await FFmpeg.GetMediaInfo(localFilePath, cancellationToken);
                var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
                if (videoStream != null)
                {
                    if (fileSystemItem.MetaProperties == null)
                    {
                        fileSystemItem.MetaProperties = new Dictionary<string, object>();
                    }

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
                if (fileSystemItem.MetaProperties == null)
                {
                    fileSystemItem.MetaProperties = new Dictionary<string, object>();
                }

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
        public long? TenantId { get; set; }
        public bool IsRoot => VirtualPath.Replace("/", "") == FileSystemId.ToString();
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

        public static FileSystemPath FromString(string s, long? tenantId)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return new FileSystemPath() { IsValid = false };
            }

            if (!s.StartsWith("/"))
            {
                s = $"/{s}";
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

            while (s.Contains("//"))
            {
                s = s.Replace("//", "/");
            }

            return new FileSystemPath()
            {
                FileSystemId = fileSystemId,
                VirtualPath = $"{s}",
                TenantId = tenantId,
                IsValid = true
            };
        }

        //public static implicit operator FileSystemPath(string s)
        //{
        //    if (string.IsNullOrWhiteSpace(s))
        //    {
        //        return new FileSystemPath() { IsValid = false };
        //    }

        //    if (!s.StartsWith("/"))
        //    {
        //        return new FileSystemPath() { IsValid = false };
        //    }

        //    var parts = s.Split("/");
        //    if (parts.Length < 2)
        //    {
        //        return new FileSystemPath() { IsValid = false };
        //    }

        //    if (!Guid.TryParse(parts[1], out var FileSystemId))
        //    {
        //        return new FileSystemPath() { IsValid = false };
        //    }

        //    return new FileSystemPath()
        //    {
        //        FileSystemId = FileSystemId,
        //        VirtualPath = $"{s}",
        //        IsValid = true
        //    };
        //}
    }

    public static class FileSystemServiceExtensions
    {
        public static void AddFileSystemService(this IServiceCollection services)
        {
            services.AddScoped<FileSystemService>();
        }
    }
}