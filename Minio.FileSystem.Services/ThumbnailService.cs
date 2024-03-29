﻿using AnimatedGif;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minio.FileSystem.Abstraction;
using Minio.FileSystem.Backend;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace Minio.FileSystem.Services
{
    public class ThumbnailService
    {
        #region Properties

        private readonly ILogger _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly FileSystemService _fileSystemService;
        private readonly IMinioClient _minioClient;

        public static Size[] SIZES = new Size[] { new Size(64, 64), new Size(128, 128), new Size(256, 256) };

        #endregion

        #region Constructor

        public ThumbnailService(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<ThumbnailService>>();
            _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            _fileSystemService = serviceProvider.GetRequiredService<FileSystemService>();
            _minioClient = serviceProvider.GetRequiredService<IMinioClient>();
        }

        #endregion

        #region ThumbnailService

        public async Task<int> CreateMissingThumbnailsAsync(CancellationToken cancellationToken = default)
        {
            var count = 0;
            FileSystemItemEntity[] fileSystemItems;
            do
            {
                fileSystemItems = await _dbContext.FileSystemItems.Where(x => x.FileSystemItemType == FileSystemItemType.File && !x.ThumbnailsProcessed).Take(10).ToArrayAsync(cancellationToken);
                foreach (var fileSystemItem in fileSystemItems)
                {
                    var thumbnails = await CreateThumbnailsAsync(fileSystemItem, cancellationToken);
                    fileSystemItem.ThumbnailsProcessed = true;
                    _dbContext.Update(fileSystemItem);
                    if (thumbnails != null)
                    {
                        _dbContext.AddRange(thumbnails);
                        count += thumbnails.Length;
                    }
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    foreach (var thumbnail in thumbnails)
                    {
                        await _processUploadAsync(thumbnail, thumbnail.Data, cancellationToken);
                    }
                }
            } while (fileSystemItems.Any() && !cancellationToken.IsCancellationRequested);
            return count;
        }

        public async Task<ThumbnailEntity[]> CreateThumbnailsAsync(FileSystemItemEntity fileSystemItem, CancellationToken cancellationToken = default)
        {
            if (fileSystemItem.ThumbnailsProcessed)
            {
                return null;
            }

            return await CreateThumbnailsAsync(fileSystemItem, SIZES, cancellationToken);
        }

        public async Task<ThumbnailEntity[]> CreateThumbnailAsync(FileSystemItemEntity fileSystemItem, int width, int height, CancellationToken cancellationToken = default)
        {
            if (fileSystemItem.ContentType.StartsWith("image/"))
            {
                var thumbnail = await _createImageThumbnailAsync(fileSystemItem, width, height, cancellationToken);
                if (thumbnail != null)
                {
                    return new ThumbnailEntity[] { thumbnail };
                }
                return new ThumbnailEntity[0];
            }
            else if (fileSystemItem.ContentType.StartsWith("video/"))
            {
                return await _createVideoThumbnailsAsync(fileSystemItem, width, height, cancellationToken);
            }
            // TODO Powerpoint
            return null;
        }

        public async Task<ThumbnailEntity[]> CreateThumbnailsAsync(FileSystemItemEntity fileSystemItem, IEnumerable<Size> sizes, CancellationToken cancellationToken = default)
        {
            var result = new List<ThumbnailEntity>();
            foreach (var size in sizes)
            {
                var thumbnails = await CreateThumbnailAsync(fileSystemItem, size.Width, size.Height, cancellationToken);
                if (thumbnails != null)
                {
                    result.AddRange(thumbnails);
                }
            }
            return result.ToArray();
        }

        #endregion

        #region Image Processing



        public async Task<byte[]> ResizeAsync(FileSystemItemEntity fileSystemItem, int width, int height, CancellationToken cancellationToken = default)
        {
            if (fileSystemItem == null)
            {
                return null;
            }

            if (!MinioConstants.ResizableImageMimeTypes.Contains(fileSystemItem.ContentType))
            {
                return null;
            }

            using (var stream = new MemoryStream())
            {
                await _fileSystemService.CopyToStreamAsync(fileSystemItem, stream, cancellationToken);
                stream.Seek(0, SeekOrigin.Begin);
                return Resize(stream, width, height);
            }
        }

        public byte[] Resize(Stream stream, int width, int height)
        {
            using (var image = Image.FromStream(stream))
            {
                return Resize(image, width, height);
            }
        }

        public byte[] Resize(Image image, int width, int height)
        {
            int quality = 75;
            int imageWidth = image.Width;
            int imageHeight = image.Height;

            if (imageWidth > width && imageHeight > height)
            {
                if (image.Width > image.Height)
                {
                    imageWidth = width;
                    imageHeight = Convert.ToInt32(image.Height * width / (double)image.Width);
                }
                else
                {
                    imageWidth = Convert.ToInt32(image.Width * height / (double)image.Height);
                    imageHeight = height;
                }
            }

            var resized = new Bitmap(imageWidth, imageHeight);
            using (var graphics = Graphics.FromImage(resized))
            {
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.DrawImage(image, 0, 0, imageWidth, imageHeight);
                using (var output = new MemoryStream())
                {
                    var qualityParamId = Encoder.Quality;
                    var encoderParameters = new EncoderParameters(1);
                    encoderParameters.Param[0] = new EncoderParameter(qualityParamId, quality);
                    var codec = ImageCodecInfo.GetImageDecoders().FirstOrDefault(codec => codec.FormatID == ImageFormat.Png.Guid);
                    resized.Save(output, codec, encoderParameters);

                    output.Seek(0, SeekOrigin.Begin);
                    return output.ToArray();
                }
            }
        }

        private async Task<ThumbnailEntity> _createImageThumbnailAsync(FileSystemItemEntity fileSystemItem, int width, int height, CancellationToken cancellationToken = default)
        {
            if (fileSystemItem == null || fileSystemItem.ThumbnailsProcessed)
            {
                return null;
            }

            using (var stream = new MemoryStream())
            {
                var success = await _fileSystemService.CopyToStreamAsync(fileSystemItem, stream, cancellationToken);
                if (!success)
                {
                    return null;
                }
                stream.Seek(0, SeekOrigin.Begin);
                using (var image = Image.FromStream(stream))
                {
                    int quality = 75;
                    int imageWidth = image.Width;
                    int imageHeight = image.Height;

                    if (width > imageHeight && height > imageHeight)
                    {
                        return null;
                    }

                    if (imageWidth > width && imageHeight > height)
                    {
                        if (image.Width > image.Height)
                        {
                            imageWidth = width;
                            imageHeight = Convert.ToInt32(image.Height * width / (double)image.Width);
                        }
                        else
                        {
                            imageWidth = Convert.ToInt32(image.Width * height / (double)image.Height);
                            imageHeight = height;
                        }
                    }

                    var resized = new Bitmap(imageWidth, imageHeight);
                    using (var graphics = Graphics.FromImage(resized))
                    {
                        graphics.CompositingQuality = CompositingQuality.HighSpeed;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.DrawImage(image, 0, 0, imageWidth, imageHeight);
                        using (var output = new MemoryStream())
                        {
                            var qualityParamId = Encoder.Quality;
                            var encoderParameters = new EncoderParameters(1);
                            encoderParameters.Param[0] = new EncoderParameter(qualityParamId, quality);
                            var codec = ImageCodecInfo.GetImageDecoders().FirstOrDefault(codec => codec.FormatID == ImageFormat.Png.Guid);
                            resized.Save(output, codec, encoderParameters);

                            output.Seek(0, SeekOrigin.Begin);

                            var thumbnail = new ThumbnailEntity()
                            {
                                Width = width,
                                Height = height,
                                FileSystemItemId = fileSystemItem.Id,
                                ContentType = "image/png",
                                ThumbnailType = ThumbnailType.Image,
                                SizeInBytes = output.Length,
                                TenantId = fileSystemItem.TenantId,
                                Data = output.ToArray()
                            };

                            //await _processUploadAsync(thumbnail, output, cancellationToken);
                            return thumbnail;
                        }
                    }
                }
            }
        }

        #endregion

        #region Video Processing

        private async Task<ThumbnailEntity[]> _createVideoThumbnailsAsync(FileSystemItemEntity fileSystemItem, int width, int height, CancellationToken cancellationToken = default)
        {
            var frames = await _extractFramesFromVideoAsync(fileSystemItem, cancellationToken);
            var firstImage = frames.ExtractedImages.FirstOrDefault();
            var result = new List<ThumbnailEntity>();
            var localPath = Path.GetTempFileName();

            try
            {
                using (var fileStream = File.OpenRead(firstImage.Path))
                {
                    var bytes = Resize(fileStream, width, height);

                    var pngThumbnail = new ThumbnailEntity()
                    {
                        Width = width,
                        Height = height,
                        FileSystemItemId = fileSystemItem.Id,
                        ContentType = "image/png",
                        ThumbnailType = ThumbnailType.Image,
                        SizeInBytes = bytes.Length,
                        TenantId = fileSystemItem.TenantId,
                        Data = bytes
                    };

                    //using (var mem = new MemoryStream())
                    //{
                    //    mem.Write(bytes);
                    //    mem.Seek(0, SeekOrigin.Begin);
                    //    await _processUploadAsync(thumbnail, mem, cancellationToken);
                    //}
                    result.Add(pngThumbnail);
                }

                using (var gif = AnimatedGif.AnimatedGif.Create(localPath, 1000))
                {

                    foreach (var image in frames.ExtractedImages)
                    {
                        var img = Image.FromFile(image.Path);
                        var bytes = Resize(img, width, height);
                        using (var mem = new MemoryStream())
                        {
                            mem.Write(bytes);
                            mem.Seek(0, SeekOrigin.Begin);
                            var resized = Image.FromStream(mem);
                            gif.AddFrame(resized, delay: -1, quality: GifQuality.Bit8);
                        }
                    }
                }

                var gifBytes = File.ReadAllBytes(localPath);
                var thumbnail = new ThumbnailEntity()
                {
                    Width = width,
                    Height = height,
                    FileSystemItemId = fileSystemItem.Id,
                    ContentType = "image/gif",
                    ThumbnailType = ThumbnailType.Gif,
                    SizeInBytes = gifBytes.Length,
                    TenantId = fileSystemItem.TenantId,
                    Data = gifBytes
                };
                result.Add(thumbnail);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Unable to create thumbnails for {fileSystemItem?.Id}");
            }
            finally
            {
                if (File.Exists(frames.Path))
                {
                    File.Delete(frames.Path);
                }

                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }
            }


            return result.ToArray();
        }

        public async Task<ExtractResult> _extractFramesFromVideoAsync(FileSystemItemEntity fileSystemItem, CancellationToken cancellationToken = default)
        {
            var basePath = Path.Combine(Environment.CurrentDirectory, "ffmpeg", "conversion", "extract_multiple", fileSystemItem.Id.ToString());
            Func<string, string> fileNameBuilder = (number) => Path.Combine(basePath, $"img_no{number}.png");

            if (!Directory.Exists(basePath))
            {
                new DirectoryInfo(basePath).Create();
            }

            var tempPath = Path.Combine(basePath, $"temp_{fileSystemItem.Id}.mp4");
            using (var fileStream = File.OpenWrite(tempPath))
            {
                await _fileSystemService.CopyToStreamAsync(fileSystemItem, fileStream, cancellationToken);
            }

            var result = new ExtractResult();
            try
            {
                var info = await FFmpeg.GetMediaInfo(tempPath);
                IVideoStream videoStream = info.VideoStreams.First()?.SetCodec(VideoCodec.png);

                _logger.LogInformation($"-----------------------------");
                _logger.LogInformation($"Path: {info.Path}");
                _logger.LogInformation($"Duration: {info.Duration}");
                _logger.LogInformation($"VideoStreams: {info.VideoStreams?.Count()}");
                _logger.LogInformation($"-----------------------------");
                _logger.LogInformation($"Video Stream -> Duration: {videoStream.Duration}");
                _logger.LogInformation($"Video Stream -> Codec: {videoStream.Codec}");
                _logger.LogInformation($"Video Stream -> Bitrate: {videoStream.Bitrate}");
                _logger.LogInformation($"Video Stream -> Framerate: {videoStream.Framerate}");
                _logger.LogInformation($"Video Stream -> PixelFormat: {videoStream.PixelFormat}");
                _logger.LogInformation($"Video Stream -> StreamType: {videoStream.StreamType}");
                _logger.LogInformation($"-----------------------------");

                var everyNth = (int)videoStream.Framerate * 10;

                _logger.LogInformation($"EveryNth: {everyNth}");
                _logger.LogInformation($"-----------------------------");

                await FFmpeg.Conversions.New()
                        .AddStream(videoStream)
                        .ExtractEveryNthFrame(everyNth, fileNameBuilder)
                        .Start();

                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                var files = Directory.EnumerateFiles(basePath).ToArray();
                result = new ExtractResult()
                {
                    Path = basePath,
                    ExtractedImages = new List<ExtractedImage>()
                };

                for (var i = 0; i < files.Length; i++)
                {
                    result.ExtractedImages.Add(new ExtractedImage()
                    {
                        Number = i + 1,
                        Path = files[i],
                        PlaybackTime = (videoStream.Duration / files.Length) * i
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error reading image from video file", ex);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                if (File.Exists(basePath))
                {
                    File.Delete(basePath);
                }
            }

            return result;
        }

        public struct ExtractResult
        {
            public string Path { get; set; }
            public List<ExtractedImage> ExtractedImages { get; set; }
        }

        public struct ExtractedImage
        {
            public string Path { get; set; }
            public int Number { get; set; }
            public TimeSpan PlaybackTime { get; set; }
        }

        #endregion

        #region Util

        private async Task _processUploadAsync(ThumbnailEntity thumbnail, byte[] data, CancellationToken cancellationToken = default)
        {
            using (var stream = new MemoryStream())
            {
                stream.Write(data);
                stream.Seek(0, SeekOrigin.Begin);
                await _processUploadAsync(thumbnail, stream, cancellationToken);
            }
        }

        private async Task _processUploadAsync(ThumbnailEntity thumbnail, Stream stream, CancellationToken cancellationToken = default)
        {
            await _minioClient.PutObjectAsync(thumbnail, stream, true, cancellationToken);
        }

        #endregion
    }
}
