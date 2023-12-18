using Minio.FileSystem.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Minio.FileSystem.Abstraction
{
    public interface IFileSystemClient
    {
        /// <summary>
        /// /filesystem/index
        /// </summary>
        Task<bool> TestAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/index
        /// </summary>
        Task<bool> TestApiKeyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/get
        /// </summary>
        Task<FileSystemItem> GetAsync(GetModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/getList
        /// </summary>
        Task<FileSystemItem[]> GetListAsync(GetListModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/filter
        /// </summary>
        Task<FileSystemItem[]> FilterAsync(FilterModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/filterFileSystems
        /// </summary>
        Task<Models.FileSystem[]> FilterFileSystemsAsync(FilterModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/upload
        /// </summary>
        Task<FileSystemItem> UploadAsync(UploadModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/getFileSystems
        /// </summary>
        Task<Models.FileSystem[]> GetFileSystemsAsync(GetFileSystemsModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/getAllFileSystems
        /// </summary>
        Task<FileSystemItem> GetAllFileSystemsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/createDirectory
        /// </summary>
        Task<FileSystemItem> CreateDirectoryAsync(CreateDirectoryModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/createLink
        /// </summary>
        Task<FileSystemItem> CreateLinkAsync(CreateLinkModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/getSize
        /// </summary>
        Task<long> GetSizeAsync(GetSizeModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/delete
        /// </summary>
        Task<Guid?> DeleteAsync(DeleteModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/move
        /// </summary>
        Task<FileSystemItem> MoveAsync(MoveModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/move
        /// </summary>
        Task<FileSystemItem> CreateZipAsync(CreateZipModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/createFileSystem
        /// </summary>
        Task<Models.FileSystem> CreateFileSystemAsync(CreateFileSystemModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/renameFileSystem
        /// </summary>
        Task<Models.FileSystem> RenameFileSystemAsync(RenameFileSystemModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/deleteFileSystem
        /// </summary>
        Task<Guid?> DeleteFileSystemAsync(DeleteFileSystemModel model, CancellationToken cancellationToken = default);

        /// <summary>
        /// /filesystem/download?id={id}
        /// </summary>
        Task<Stream> DownloadAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
