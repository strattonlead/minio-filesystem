using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minio.FileSystem.Backend;
using Minio.FileSystem.Services;
using Minio.FileSystem.Services.Authorization;
using Minio.FileSystem.WebApi.Models;
using Muffin.Tenancy.Services.Abstraction;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Minio.FileSystem.WebApi.Controllers
{
    [ApiController]
    public class FileSystemController : ControllerBase
    {
        private readonly FileSystemService _fileSystemService;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ITenantProvider _tenantProvider;
        private readonly ApplicationOptions _options;
        private readonly FileCacheService _cache;
        private CancellationToken _cancellationToken => _applicationLifetime.ApplicationStopping;

        public FileSystemController(IServiceProvider serviceProvider)
        {
            _fileSystemService = serviceProvider.GetRequiredService<FileSystemService>();
            _applicationLifetime = serviceProvider.GetRequiredService<IHostApplicationLifetime>();
            _tenantProvider = serviceProvider.GetRequiredService<ITenantProvider>();
            _options = serviceProvider.GetRequiredService<ApplicationOptions>();
            _cache = serviceProvider.GetRequiredService<FileCacheService>();
        }

        [HttpGet, Route("/filesystem")]
        public string Index()
        {
            return "Minio.FileSystem running...";
        }

        [HttpGet, ApiKey, Route("/filesystem/test")]
        public string TestApiKey()
        {
            return "Minio.FileSystem running...";
        }

        [HttpPost, ApiKey, Route("/filesystem/get")]
        public async Task<FileSystemItemEntity> GetAsync([FromBody] GetModel model)
        {
            if (model.Id.HasValue)
            {
                return await _fileSystemService.GetAsync(model.Id.Value, _cancellationToken);
            }
            var path = FileSystemPath.FromString(model.VirtualPath, model.TenantId);
            return await _fileSystemService.FindAsync(path, _cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/getThumbnail")]
        public async Task<ThumbnailEntity> GetThumbnailAsync([FromBody] GetThumbnailModel model)
        {
            return await _fileSystemService.GetThumbnailAsync(model.Id, _cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/getList")]
        public async Task<FileSystemItemEntity[]> ListAsync([FromBody] GetListModel model)
        {
            if (model.Id.HasValue)
            {
                return await _fileSystemService.GetListAsync(model.Id.Value, _cancellationToken);
            }
            var path = FileSystemPath.FromString(model.VirtualPath, model.TenantId);
            return await _fileSystemService.GetListAsync(path, _cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/getFileSystems")]
        public async Task<FileSystemEntity[]> GetFileSystemsAsync([FromBody] GetFileSystemsModel model)
        {
            return await _fileSystemService.GetFileSystemsAsync(model.TenantId, _cancellationToken);
        }

        [HttpGet, ApiKey, Route("/filesystem/getAllFileSystems")]
        public async Task<FileSystemEntity[]> GetFileSystemsAsync()
        {
            return await _fileSystemService.GetAllFileSystemsAsync(_cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/filter")]
        public async Task<FileSystemItemEntity[]> FilterAsync([FromBody] FilterModel model)
        {
            return await _fileSystemService.FilterAsync(model.Filter, model.VirtualPath, model.TenantId, _cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/filterFileSystems")]
        public async Task<FileSystemEntity[]> FilterFileSystemsAsync([FromBody] FilterModel model)
        {
            return await _fileSystemService.FilterFileSystemsAsync(model.Filter, model.TenantId, _cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/upload")]
        public async Task<FileSystemItemEntity> UploadAsync([FromForm] UploadModel model)
        {
            var path = FileSystemPath.FromString(model.VirtualPath, model.TenantId);
            return await _fileSystemService.UploadAsync(path, model.File, _cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/createLink")]
        public async Task<FileSystemItemEntity> CreateLinkAsync([FromBody] CreateLinkModel model)
        {
            var path = FileSystemPath.FromString(model.VirtualPath, model.TenantId);
            return await _fileSystemService.CreateLinkAsync(path, model.Url, _cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/getSize")]
        public async Task<long> GetSizeAsync([FromBody] GetSizeModel model)
        {
            var path = FileSystemPath.FromString(model.VirtualPath, model.TenantId);
            return await _fileSystemService.GetSizeAsync(path, _cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/createDirectory")]
        public async Task<FileSystemItemEntity> CreateDirectoryAsync([FromBody] CreateDirectoryModel model)
        {
            var path = FileSystemPath.FromString(model.VirtualPath, model.TenantId);
            return await _fileSystemService.CreateDirectoryAsync(path, _cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/delete")]
        public async Task<Guid?> DeleteAsync([FromBody] DeleteModel model)
        {
            if (model.Id.HasValue)
            {
                return await _fileSystemService.DeleteAsync(model.Id.Value, _cancellationToken);
            }

            var path = FileSystemPath.FromString(model.VirtualPath, model.TenantId);
            return await _fileSystemService.DeleteAsync(path, _cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/move")]
        public async Task<FileSystemItemEntity> MoveAsync([FromBody] MoveModel model)
        {
            var sourcePath = FileSystemPath.FromString(model.SourcePath, model.TenantId);
            var destinationPath = FileSystemPath.FromString(model.DestinationPath, model.TenantId);
            return await _fileSystemService.MoveAsync(sourcePath, destinationPath, model.Override, _cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/createFileSystem")]
        public async Task<FileSystemEntity> CreateFileSystemAsync([FromBody] CreateFileSystemModel model)
        {
            return await _fileSystemService.CreateFileSystemAsync(model.Name, model.TenantId, _cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/renameFileSystem")]
        public async Task<FileSystemEntity> RenameFileSystemAsync([FromBody] RenameFileSystemModel model)
        {
            return await _fileSystemService.RenameFileSystemAsync(model.Id, model.Name, _cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/deleteFileSystem")]
        public async Task<Guid?> DeleteFileSystemAsync([FromBody] DeleteFileSystemModel model)
        {
            return await _fileSystemService.DeleteFileSystemAsync(model.Id, _cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/createZip")]
        public async Task<FileSystemItemEntity> CreateZipAsync([FromBody] CreateZipModel model)
        {
            var path = FileSystemPath.FromString(model.VirtualPath, model.TenantId);

            if (model.Ids != null)
            {
                return await _fileSystemService.CreateZipAsync(model.Ids, path, _cancellationToken);
            }

            var ids = await _fileSystemService.GetIdsAsync(model.VirtualPaths, model.TenantId, _cancellationToken);
            return await _fileSystemService.CreateZipAsync(ids, path, _cancellationToken);
        }

        [HttpPost, ApiKey, Route("/filesystem/unzip")]
        public async Task<bool> UnzipAsync([FromBody] UnzipModel model)
        {
            if (model.Id.HasValue)
            {
                return await _fileSystemService.UnzipAsync(model.Id.Value, _cancellationToken);
            }
            return false;
        }

        [HttpGet, ApiKey, Route("/filesystem/download")]
        public async Task<IActionResult> DownloadAsync([FromQuery] Guid id)
        {
            var fileSystemItem = await _fileSystemService.GetAsync(id, _cancellationToken);
            if (fileSystemItem == null)
            {
                return NotFound();
            }

            var syncIOFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
            if (syncIOFeature != null)
            {
                syncIOFeature.AllowSynchronousIO = true;
            }

            if (fileSystemItem.IsDirectory)
            {
                using (var mem = new MemoryStream())
                {
                    await _fileSystemService.ZipToStreamAsync(fileSystemItem, mem, _cancellationToken);
                    return File(mem.ToArray(), "application/zip", $"{fileSystemItem.Name}.zip");
                }
            }

            if (_options.FileCacheEnabled)
            {
                if (_cache.IsCached(fileSystemItem))
                {
                    var readStream = _cache.OpenReadStream(fileSystemItem);
                    return File(readStream, fileSystemItem.ContentType, fileSystemItem.Name);
                }

                using (var writeStream = _cache.OpenWriteStream(fileSystemItem))
                {
                    await _fileSystemService.CopyToStreamAsync(fileSystemItem, writeStream, _cancellationToken);
                }

                if (_cache.IsCached(fileSystemItem))
                {
                    var readStream = _cache.OpenReadStream(fileSystemItem);
                    return File(readStream, fileSystemItem.ContentType, fileSystemItem.Name);
                }
            }

            using (var mem = new MemoryStream())
            {
                await _fileSystemService.CopyToStreamAsync(fileSystemItem, mem, _cancellationToken);
                return File(mem.ToArray(), fileSystemItem.ContentType, fileSystemItem.Name);
            }
        }

        [HttpGet, ApiKey, Route("/filesystem/thumb")]
        public async Task<IActionResult> DownloadThumbnailAsync([FromQuery] Guid id)
        {
            var thumbnail = await _fileSystemService.GetThumbnailAsync(id, _cancellationToken);
            if (thumbnail == null)
            {
                return NotFound();
            }

            var syncIOFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
            if (syncIOFeature != null)
            {
                syncIOFeature.AllowSynchronousIO = true;
            }

            if (_options.FileCacheEnabled)
            {
                if (_cache.IsCached(thumbnail))
                {
                    var readStream = _cache.OpenReadStream(thumbnail);
                    return File(readStream, thumbnail.ContentType, thumbnail.StoragePath);
                }

                using (var writeStream = _cache.OpenWriteStream(thumbnail))
                {
                    await _fileSystemService.CopyToStreamAsync(thumbnail, writeStream, _cancellationToken);
                }

                if (_cache.IsCached(thumbnail))
                {
                    var readStream = _cache.OpenReadStream(thumbnail);
                    return File(readStream, thumbnail.ContentType, thumbnail.StoragePath);
                }
            }

            using (var mem = new MemoryStream())
            {
                await _fileSystemService.CopyToStreamAsync(thumbnail, mem, _cancellationToken);
                return File(mem.ToArray(), thumbnail.ContentType, thumbnail.StoragePath);
            }
        }

        [HttpGet, Authorize, AllowAnonymous, Route("/{fileSystemId:guid}/{**catchAll}")]
        public async Task<IActionResult> GetContentAsync(Guid? fileSystemId)
        {
            var path = FileSystemPath.FromString(Request.Path.Value, _tenantProvider?.ActiveTenant?.Id);
            if (!path.IsValid)
            {
                return BadRequest("Not a valid path. (e.g. /00000000-0000-0000-0000-000000000000/directory/file.txt");
            }

            var fileSystemItem = await _fileSystemService.FindAsync(path, _cancellationToken);
            if (fileSystemItem == null)
            {
                return NotFound($"File not found at path {path.VirtualPath}");
            }

            if (_options.TenancyEnabled && fileSystemItem.TenantId.HasValue && fileSystemItem.TenantId != _tenantProvider.ActiveTenant?.Id)
            {
                return Unauthorized("Access denied");
            }

            var syncIOFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
            if (syncIOFeature != null)
            {
                syncIOFeature.AllowSynchronousIO = true;
            }

            if (_options.FileCacheEnabled)
            {
                if (_cache.IsCached(fileSystemItem))
                {
                    var readStream = _cache.OpenReadStream(fileSystemItem);
                    return File(readStream, fileSystemItem.ContentType, fileSystemItem.Name);
                }

                using (var writeStream = _cache.OpenWriteStream(fileSystemItem))
                {
                    await _fileSystemService.CopyToStreamAsync(fileSystemItem, writeStream, _cancellationToken);
                }

                if (_cache.IsCached(fileSystemItem))
                {
                    var readStream = _cache.OpenReadStream(fileSystemItem);
                    return File(readStream, fileSystemItem.ContentType, fileSystemItem.Name);
                }
            }

            using (var mem = new MemoryStream())
            {
                await _fileSystemService.CopyToStreamAsync(fileSystemItem, mem, _cancellationToken);
                mem.Seek(0, SeekOrigin.Begin);

                return File(mem, fileSystemItem.ContentType, fileSystemItem.Name);
            }
        }
    }
}