using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minio.FileSystem.Abstraction;
using Minio.FileSystem.Backend;
using Muffin.Tenancy.Services.Abstraction;
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
        private readonly ITenantProvider _tenantProvider;
        private readonly ApplicationDbContext _dbContext;
        private readonly FileSystemService _fileSystemService;
        private readonly IMinioClient _minioClient;

        public static Size[] SIZES = new Size[] { new Size(64, 64), new Size(128, 128), new Size(256, 256) };

        #endregion

        #region Constructor

        public ThumbnailService(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<ThumbnailService>>();
            _tenantProvider = serviceProvider.GetRequiredService<ITenantProvider>();
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
                fileSystemItems = await _dbContext.FileSystemItems.Where(x => !x.ThumbnailsProcessed).Take(10).ToArrayAsync(cancellationToken);
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
                return new ThumbnailEntity[] { thumbnail };
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
        }

        private async Task<ThumbnailEntity> _createImageThumbnailAsync(FileSystemItemEntity fileSystemItem, int width, int height, CancellationToken cancellationToken = default)
        {
            if (fileSystemItem == null || fileSystemItem.ThumbnailsProcessed)
            {
                return null;
            }

            using (var stream = new MemoryStream())
            {
                await _fileSystemService.CopyToStreamAsync(fileSystemItem, stream, cancellationToken);
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
                                TenantId = fileSystemItem.TenantId
                            };

                            await _processUploadAsync(thumbnail, output, cancellationToken);
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

            using (var fileStream = File.OpenRead(firstImage.Path))
            {
                var bytes = Resize(fileStream, width, height);

                var thumbnail = new ThumbnailEntity()
                {
                    Width = width,
                    Height = height,
                    FileSystemItemId = fileSystemItem.Id,
                    ContentType = "image/png",
                    ThumbnailType = ThumbnailType.Image,
                    SizeInBytes = bytes.Length,
                    TenantId = fileSystemItem.TenantId
                };

                using (var mem = new MemoryStream())
                {
                    mem.Write(bytes);
                    mem.Seek(0, SeekOrigin.Begin);
                    await _processUploadAsync(thumbnail, mem, cancellationToken);
                }
                result.Add(thumbnail);
            }

            using (var collection = new MagickImageCollection())
            {
                foreach (var image in frames.ExtractedImages)
                {
                    collection.Add(image.Path);
                }

                foreach (var item in collection)
                {
                    item.AnimationDelay = 1000;
                    item.Resize(width, height);
                }

                var settings = new QuantizeSettings();
                collection.Quantize(settings);
                collection.Optimize();

                var path = Path.Combine(frames.Path, "");
                using (var stream = new MemoryStream())
                {
                    collection.Write(stream);
                    stream.Seek(0, SeekOrigin.Begin);

                    var thumbnail = new ThumbnailEntity()
                    {
                        Width = width,
                        Height = height,
                        FileSystemItemId = fileSystemItem.Id,
                        ContentType = "image/gif",
                        ThumbnailType = ThumbnailType.Gif,
                        SizeInBytes = stream.Length,
                        TenantId = fileSystemItem.TenantId
                    };

                    await _processUploadAsync(thumbnail, stream, cancellationToken);
                    result.Add(thumbnail);
                }
            }

            if (File.Exists(frames.Path))
            {
                File.Delete(frames.Path);
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

                var everyNth = (int)videoStream.Framerate * 2;

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

        private async Task _processUploadAsync(ThumbnailEntity thumbnail, Stream stream, CancellationToken cancellationToken = default)
        {
            _tenantProvider.SetTenant(thumbnail.TenantId);
            await _minioClient.PutObjectAsync(thumbnail, stream, true, cancellationToken);
            _tenantProvider.RestoreTenancy();
        }

        #endregion
    }
}
