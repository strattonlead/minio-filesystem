using Minio.FileSystem.Abstraction;
using Minio.FileSystem.Client.Util;
using Minio.FileSystem.Models;
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
    public class FileSystemClient : IFileSystemClient
    {
        private readonly HttpClient _httpClient;

        public FileSystemClient(FileSystemClientOptions options)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.BaseUrl)
            };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", options.ApiKey);
        }

        #region Actions

        /// <summary>
        /// /filesystem/index
        /// </summary>
        public async Task<bool> TestAsync(CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync("/filesystem");
            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// /filesystem/index
        /// </summary>
        public async Task<bool> TestApiKeyAsync(CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync("/filesystem/test");
            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// /filesystem/get
        /// </summary>
        public async Task<FileSystemItem> GetAsync(GetModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/get", model, cancellationToken);
            return await _handleResponse<FileSystemItem>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/getThumbnail
        /// </summary>
        public async Task<Thumbnail> GetAsync(GetThumbnailModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/getThumbnail", model, cancellationToken);
            return await _handleResponse<Thumbnail>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/getList
        /// </summary>
        public async Task<FileSystemItem[]> GetListAsync(GetListModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/getList", model, cancellationToken);
            return await _handleResponse<FileSystemItem[]>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/filter
        /// </summary>
        public async Task<FileSystemItem[]> FilterAsync(FilterModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/filter", model, cancellationToken);
            return await _handleResponse<FileSystemItem[]>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/filterFileSystems
        /// </summary>
        public async Task<Models.FileSystem[]> FilterFileSystemsAsync(FilterModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/filterFileSystems", model, cancellationToken);
            return await _handleResponse<Models.FileSystem[]>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/upload
        /// </summary>
        public async Task<FileSystemItem> UploadAsync(UploadModel model, CancellationToken cancellationToken = default)
        {
            return await UploadAsync(model, null, cancellationToken);
        }

        /// <summary>
        /// /filesystem/upload
        /// </summary>
        public async Task<FileSystemItem> UploadAsync(UploadModel model, EventHandler<UploadProgress> uploadProgressChanged, CancellationToken cancellationToken = default)
        {
            var progress = new Progress<UploadProgress>();
            if (uploadProgressChanged != null)
            {
                progress.ProgressChanged += (e, p) => { uploadProgressChanged?.Invoke(this, p); };
            }
            using (var content = new MultipartFormDataContent())
            using (var streamContent = new ProgressableStreamContent(model.Stream, 4096, progress))
            {
                content.Add(streamContent, "file", model.Name);
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(model.ContentType);

                var virtualPathContent = new StringContent(model.VirtualPath, Encoding.UTF8, "text/plain");
                content.Add(virtualPathContent, "virtualPath");

                if (model.TenantId.HasValue)
                {
                    var tenantIdContent = new StringContent(model.TenantId.Value.ToString(), Encoding.UTF8, "text/plain");
                    content.Add(tenantIdContent, "tenantId");
                }

                var response = await _httpClient.PostAsync("/filesystem/upload", content, cancellationToken);
                return await _handleResponse<FileSystemItem>(response, cancellationToken);
            }
        }

        /// <summary>
        /// /filesystem/getFileSystems
        /// </summary>
        public async Task<Models.FileSystem[]> GetFileSystemsAsync(GetFileSystemsModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/getFileSystems", model, cancellationToken);
            return await _handleResponse<Models.FileSystem[]>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/getAllFileSystems
        /// </summary>
        public async Task<FileSystemItem> GetAllFileSystemsAsync(CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync("/filesystem/getAllFileSystems", cancellationToken);
            return await _handleResponse<FileSystemItem>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/createDirectory
        /// </summary>
        public async Task<FileSystemItem> CreateDirectoryAsync(CreateDirectoryModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/createDirectory", model, cancellationToken);
            return await _handleResponse<FileSystemItem>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/createLink
        /// </summary>
        public async Task<FileSystemItem> CreateLinkAsync(CreateLinkModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/createLink", model, cancellationToken);
            return await _handleResponse<FileSystemItem>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/getSize
        /// </summary>
        public async Task<long> GetSizeAsync(GetSizeModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/getSize", model, cancellationToken);
            return await _handleResponse<long>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/delete
        /// </summary>
        public async Task<Guid?> DeleteAsync(DeleteModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/delete", model, cancellationToken);
            return await _handleResponse<Guid?>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/move
        /// </summary>
        public async Task<FileSystemItem> MoveAsync(MoveModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/move", model, cancellationToken);
            return await _handleResponse<FileSystemItem>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/createZip
        /// </summary>
        public async Task<FileSystemItem> CreateZipAsync(CreateZipModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/createZip", model, cancellationToken);
            return await _handleResponse<FileSystemItem>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/unzip
        /// </summary>
        public async Task<FileSystemItem> UnzipAsync(UnzipModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/unzip", model, cancellationToken);
            return await _handleResponse<FileSystemItem>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/createFileSystem
        /// </summary>
        public async Task<Models.FileSystem> CreateFileSystemAsync(CreateFileSystemModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/createFileSystem", model, cancellationToken);
            return await _handleResponse<Models.FileSystem>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/renameFileSystem
        /// </summary>
        public async Task<Models.FileSystem> RenameFileSystemAsync(RenameFileSystemModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/renameFileSystem", model, cancellationToken);
            return await _handleResponse<Models.FileSystem>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/deleteFileSystem
        /// </summary>
        public async Task<Guid?> DeleteFileSystemAsync(DeleteFileSystemModel model, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/filesystem/deleteFileSystem", model, cancellationToken);
            return await _handleResponse<Guid?>(response, cancellationToken);
        }

        /// <summary>
        /// /filesystem/download?id={id}
        /// </summary>
        public async Task<Stream> DownloadAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"/filesystem/download?id={id}", cancellationToken);
            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }

        /// <summary>
        /// /filesystem/thumb?id={id}
        /// </summary>
        public async Task<Stream> DownloadThumbnailAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"/filesystem/thumb?id={id}", cancellationToken);
            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }

        #endregion

        #region Helpers

        private async Task<T> _handleResponse<T>(HttpResponseMessage response, CancellationToken cancellationToken = default)
        {
            try
            {

                return await response.Content.ReadFromJsonAsync<T>((JsonSerializerOptions)null, cancellationToken);
            }
            catch (Exception e)
            {
                var rawResponse = await response.Content.ReadAsStringAsync();
                throw new Exception(rawResponse, e);
            }
        }

        #endregion
    }


}