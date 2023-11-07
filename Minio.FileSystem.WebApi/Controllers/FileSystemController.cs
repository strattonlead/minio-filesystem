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
            var path = FileSystemPath.FromString(model.VirtualPath, model.TenantId);
            return await _fileSystemService.FindAsync(path);
        }

        [HttpPost, ApiKey, Route("/filesystem/getFileSystems")]
        public async Task<FileSystemEntity[]> GetFileSystemsAsync([FromBody] GetFileSystemsModel model)
        {
            return await _fileSystemService.GetFileSystemsAsync(model.TenantId, _applicationLifetime.ApplicationStopping);
        }

        [HttpGet, ApiKey, Route("/filesystem/getAllFileSystems")]
        public async Task<FileSystemEntity[]> GetFileSystemsAsync()
        {
            return await _fileSystemService.GetAllFileSystemsAsync(_applicationLifetime.ApplicationStopping);
        }

        [HttpPost, ApiKey, Route("/filesystem/filter")]
        public async Task<FileSystemItemEntity[]> FilterAsync([FromBody] FilterModel model)
        {
            return await _fileSystemService.FilterAsync(model.Filter, model.VirtualPath, model.TenantId, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, ApiKey, Route("/filesystem/filterFileSystems")]
        public async Task<FileSystemEntity[]> FilterFileSystemsAsync([FromBody] FilterModel model)
        {
            return await _fileSystemService.FilterFileSystemsAsync(model.Filter, model.TenantId, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, ApiKey, Route("/filesystem/upload")]
        public async Task<FileSystemItemEntity> UploadAsync([FromForm] UploadModel model)
        {
            var path = FileSystemPath.FromString(model.VirtualPath, model.TenantId);
            return await _fileSystemService.UploadAsync(path, model.File, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, ApiKey, Route("/filesystem/createLink")]
        public async Task<FileSystemItemEntity> CreateLinkAsync([FromBody] CreateLinkModel model)
        {
            var path = FileSystemPath.FromString(model.VirtualPath, model.TenantId);
            return await _fileSystemService.CreateLinkAsync(path, model.Url, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, ApiKey, Route("/filesystem/getSize")]
        public async Task<long> GetSizeAsync([FromBody] GetSizeModel model)
        {
            var path = FileSystemPath.FromString(model.VirtualPath, model.TenantId);
            return await _fileSystemService.GetSizeAsync(path, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, ApiKey, Route("/filesystem/createDirectory")]
        public async Task<FileSystemItemEntity> CreateDirectoryAsync([FromBody] CreateDirectoryModel model)
        {
            var path = FileSystemPath.FromString(model.VirtualPath, model.TenantId);
            return await _fileSystemService.CreateDirectoryAsync(path, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, ApiKey, Route("/filesystem/delete")]
        public async Task<Guid?> DeleteAsync([FromBody] DeleteModel model)
        {
            var path = FileSystemPath.FromString(model.VirtualPath, model.TenantId);
            return await _fileSystemService.DeleteAsync(path, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, ApiKey, Route("/filesystem/move")]
        public async Task<FileSystemItemEntity> MoveAsync([FromBody] MoveModel model)
        {
            var sourcePath = FileSystemPath.FromString(model.SourcePath, model.TenantId);
            var destinationPath = FileSystemPath.FromString(model.DestinationPath, model.TenantId);
            return await _fileSystemService.MoveAsync(sourcePath, destinationPath, model.Override, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, ApiKey, Route("/filesystem/createFileSystem")]
        public async Task<FileSystemEntity> CreateFileSystemAsync([FromBody] CreateFileSystemModel model)
        {
            return await _fileSystemService.CreateFileSystemAsync(model.Name, model.TenantId, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, ApiKey, Route("/filesystem/renameFileSystem")]
        public async Task<FileSystemEntity> RenameFileSystemAsync([FromBody] RenameFileSystemModel model)
        {
            return await _fileSystemService.RenameFileSystemAsync(model.Id, model.Name, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, ApiKey, Route("/filesystem/deleteFileSystem")]
        public async Task<Guid?> DeleteFileSystemAsync([FromBody] DeleteFileSystemModel model)
        {
            return await _fileSystemService.DeleteFileSystemAsync(model.Id, _applicationLifetime.ApplicationStopping);
        }

        [HttpGet, Authorize, AllowAnonymous, Route("/{fileSystemId:guid}/{**catchAll}")]
        public async Task<IActionResult> GetContentAsync(Guid? fileSystemId)
        {
            _tenantProvider.SetTenant(1);
            var path = FileSystemPath.FromString(Request.Path.Value, _tenantProvider?.ActiveTenant?.Id);
            if (!path.IsValid)
            {
                return BadRequest("Not a valid path. (e.g. /00000000-0000-0000-0000-000000000000/directory/file.txt");
            }

            var fileSystemItem = await _fileSystemService.FindAsync(path, _applicationLifetime.ApplicationStopping);
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
                    await _fileSystemService.CopyToStreamAsync(fileSystemItem, writeStream);
                }

                if (_cache.IsCached(fileSystemItem))
                {
                    var readStream = _cache.OpenReadStream(fileSystemItem);
                    return File(readStream, fileSystemItem.ContentType, fileSystemItem.Name);
                }
            }

            using (var mem = new MemoryStream())
            {
                await _fileSystemService.CopyToStreamAsync(fileSystemItem, mem);
                mem.Seek(0, SeekOrigin.Begin);

                return File(mem, fileSystemItem.ContentType, fileSystemItem.Name);
            }
        }
    }
}