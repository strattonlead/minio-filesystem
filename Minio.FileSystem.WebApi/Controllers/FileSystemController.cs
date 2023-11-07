using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minio.Filesystem.Backend;
using Minio.FileSystem.Services;
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

        [HttpGet, Route("/filesystem/index")]
        public string Index()
        {
            return "Minio.FileSystem running...";
        }

        [HttpPost, Route("/filesystem/get")]
        public async Task<FileSystemItemEntity> GetAsync([FromBody] GetModel model)
        {
            return await _fileSystemService.FindAsync(model.VirtualPath);
        }

        [HttpPost, Route("/filesystem/filter")]
        public async Task<FileSystemItemEntity[]> FilterAsync([FromBody] FilterModel model)
        {
            return await _fileSystemService.FilterAsync(model.Filter, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, Route("/filesystem/filterfilesystems")]
        public async Task<FileSystemEntity[]> FilterFileSystemsAsync([FromBody] FilterModel model)
        {
            return await _fileSystemService.FilterFileSystemsAsync(model.Filter, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, Route("/filesystem/upload")]
        public async Task<FileSystemItemEntity> UploadAsync([FromBody] UploadModel model)
        {
            return await _fileSystemService.UploadAsync(model.VirtualPath, model.File, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, Route("/filesystem/createdirectory")]
        public async Task<FileSystemItemEntity> CreateDirectoryAsync([FromBody] CreateDirectoryModel model)
        {
            return await _fileSystemService.CreateDirectoryAsync(model.VirtualPath, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, Route("/filesystem/delete")]
        public async Task<Guid?> DeleteAsync([FromBody] DeleteModel model)
        {
            return await _fileSystemService.DeleteAsync(model.VirtualPath, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, Route("/filesystem/move")]
        public async Task<FileSystemItemEntity> MoveAsync([FromBody] MoveModel model)
        {
            return await _fileSystemService.MoveAsync(model.SourcePath, model.DestinationPath, model.Override, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, Route("/filesystem/createfilesystem")]
        public async Task<FileSystemEntity> CreateFileSystemAsync([FromBody] CreateFileSystemModel model)
        {
            return await _fileSystemService.CreateFileSystemAsync(model.Name, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, Route("/filesystem/renamefilesystem")]
        public async Task<FileSystemEntity> RenameFileSystemAsync([FromBody] RenameFileSystemModel model)
        {
            return await _fileSystemService.RenameFileSystemAsync(model.Id, model.Name, _applicationLifetime.ApplicationStopping);
        }

        [HttpPost, Route("/filesystem/deletefilesystem")]
        public async Task<Guid?> DeleteFileSystemAsync([FromBody] DeleteFileSystemModel model)
        {
            return await _fileSystemService.DeleteFileSystemAsync(model.Id, _applicationLifetime.ApplicationStopping);
        }

        [HttpGet, Authorize, AllowAnonymous, Route("{**catchAll}")]
        public async Task<IActionResult> GetContentAsync()
        {
            var path = (FileSystemPath)Request.Path.Value;
            if (!path.IsValid)
            {
                return BadRequest("Not a valid path. (e.g. /00000000000-0000-0000-0000-00000001/directory/file.txt");
            }

            var fileSystemItem = await _fileSystemService.FindAsync(path, _applicationLifetime.ApplicationStopping);
            if (fileSystemItem == null)
            {
                return NotFound($"File not found at path {path}");
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