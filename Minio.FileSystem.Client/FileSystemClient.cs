using Minio.FileSystem.Backend;
using Minio.FileSystem.Client.Models;
using System;
using System.IO;
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
        private readonly HttpClient _httpClient;

        public FileSystemClient(FileSystemClientOptions options)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.BaseUrl)
            };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer:", options.ApiKey);
        }

        #region Actions

        /// <summary>
        /// /filesystem/index
        /// </summary>
        public async Task<bool> TestAsync()
        {
            var response = await _httpClient.GetAsync("/filesystem");
            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// /filesystem/index
        /// </summary>
        public async Task<bool> TestApiKeyAsync()
        {
            var response = await _httpClient.GetAsync("/filesystem/test");
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
        /// /filesystem/filterFileSystems
        /// </summary>
        public async Task<FileSystemEntity[]> FilterFileSystemsAsync(FilterModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/filterFileSystems", model, cancellationToken);
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
        /// /filesystem/getFileSystems
        /// </summary>
        public async Task<FileSystemEntity[]> GetFileSystemsAsync(GetFileSystemsModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/getFileSystems", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<FileSystemEntity[]>((JsonSerializerOptions)null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/getAllFileSystems
        /// </summary>
        public async Task<FileSystemItemEntity> GetAllFileSystemsAsync(CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync("/filesystem/getAllFileSystems", cancellationToken);
            return await response.Content.ReadFromJsonAsync<FileSystemItemEntity>((JsonSerializerOptions)null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/createDirectory
        /// </summary>
        public async Task<FileSystemItemEntity> CreateDirectoryAsync(CreateDirectoryModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/createDirectory", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<FileSystemItemEntity>((JsonSerializerOptions)null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/createLink
        /// </summary>
        public async Task<FileSystemItemEntity> CreateLinkAsync(CreateLinkModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/createLink", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<FileSystemItemEntity>((JsonSerializerOptions)null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/getSize
        /// </summary>
        public async Task<long> GetSizeAsync(GetSizeModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/getSize", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<long>((JsonSerializerOptions)null, cancellationToken);
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
        /// /filesystem/createFileSystem
        /// </summary>
        public async Task<FileSystemEntity> CreateFileSystemAsync(CreateFileSystemModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/createFileSystem", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<FileSystemEntity>((JsonSerializerOptions)null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/renameFileSystem
        /// </summary>
        public async Task<FileSystemEntity> RenameFileSystemAsync(RenameFileSystemModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/renameFileSystem", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<FileSystemEntity>((JsonSerializerOptions)null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/deleteFileSystem
        /// </summary>
        public async Task<Guid?> DeleteFileSystemAsync(DeleteFileSystemModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/deleteFileSystem", model, cancellationToken);
            return await response.Content.ReadFromJsonAsync<Guid?>((JsonSerializerOptions)null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/download?id={id}
        /// </summary>
        public async Task<Stream> DownloadAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"/filesystem/download?id={id}", cancellationToken);
            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }

        #endregion
    }


}