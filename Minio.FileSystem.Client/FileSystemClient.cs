using Minio.Filesystem.Backend;
using Minio.FileSystem.Client.Models;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Minio.FileSystem.Client
{
    public class FileSystemClient
    {
        private readonly FileSystemClientOptions _options;
        private readonly HttpClient _httpClient;

        public FileSystemClient(FileSystemClientOptions options)
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(options.BaseUrl);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer:", options.ApiKey);
        }

        #region Actions

        /// <summary>
        /// /filesystem/index
        /// </summary>
        public async Task<bool> TestAsync()
        {
            var response = await _httpClient.GetAsync("/filesystem/index");
            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// /filesystem/get
        /// </summary>
        public async Task<FileSystemItemEntity> GetAsync(GetModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/get", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<FileSystemItemEntity>((JsonSerializerOptions)null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/filter
        /// </summary>
        public async Task<FileSystemItemEntity[]> FilterAsync(FilterModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/filter", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<FileSystemItemEntity[]>((JsonSerializerOptions)null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/filterfilesystems
        /// </summary>
        public async Task<FileSystemEntity[]> FilterFileSystemsAsync(FilterModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/filterfilesystems", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<FileSystemEntity[]>((JsonSerializerOptions)null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/upload
        /// </summary>
        public async Task<FileSystemItemEntity> UploadAsync(UploadModel model, CancellationToken cancellationToken = default)
        {
            using (var content = new MultipartFormDataContent())
            using (var streamContent = new StreamContent(model.Stream))
            {
                content.Add(streamContent, "file", model.Name);
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(model.ContentType);

                var stringContent = new StringContent(model.VirtualPath, Encoding.UTF8, "text/plain");
                content.Add(stringContent, "virtualPath");

                var response = await _httpClient.PostAsync("/filesystem/upload", content, cancellationToken);
                return await response.Content.ReadFromJsonAsync<FileSystemItemEntity>((JsonSerializerOptions)null, cancellationToken);
            }
        }

        /// <summary>
        /// /filesystem/createdirectory
        /// </summary>
        public async Task<FileSystemItemEntity> CreateDirectoryAsync(CreateDirectoryModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/createdirectory", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<FileSystemItemEntity>((JsonSerializerOptions)null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/delete
        /// </summary>
        public async Task<Guid?> DeleteAsync(DeleteModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/delete", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<Guid?>((JsonSerializerOptions)null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/move
        /// </summary>
        public async Task<FileSystemItemEntity> MoveAsync(MoveModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/move", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<FileSystemItemEntity>((JsonSerializerOptions)null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/createfilesystem
        /// </summary>
        public async Task<FileSystemEntity> CreateFileSystemAsync(CreateFileSystemModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/createfilesystem", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<FileSystemEntity>((JsonSerializerOptions)null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/renamefilesystem
        /// </summary>
        public async Task<FileSystemEntity> RenameFileSystemAsync(RenameFileSystemModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/renamefilesystem", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<FileSystemEntity>((JsonSerializerOptions)null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/deletefilesystem
        /// </summary>
        public async Task<Guid?> DeleteFileSystemAsync(DeleteFileSystemModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/deletefilesystem", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<Guid?>((JsonSerializerOptions)null, cancellationToken);
        }

        #endregion
    }


}