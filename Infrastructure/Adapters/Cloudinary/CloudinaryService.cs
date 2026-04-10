using Application.Interfaces.Creator;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Adapters.Cloudinary
{
  /// Implement IStorageService dùng Cloudinary.
  /// Tự động convert ảnh sang WebP và tối ưu chất lượng.
  public class CloudinaryService : IStorageService
  {
    private readonly CloudinaryDotNet.Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryService> _logger;

    public CloudinaryService(IConfiguration config, ILogger<CloudinaryService> logger)
    {
      _logger = logger;

      var cloudName = config["Cloudinary:CloudName"]
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);
      var apiKey = config["Cloudinary:ApiKey"]
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);
      var apiSecret = config["Cloudinary:ApiSecret"]
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      var account = new Account(cloudName, apiKey, apiSecret);
      _cloudinary = new CloudinaryDotNet.Cloudinary(account)
      {
        Api = { Secure = true } // luôn dùng HTTPS
      };
    }

    public async Task<string> UploadAsync(
Stream stream,
string fileName,
string folder,
CancellationToken cancellationToken = default)
    {
      var uploadParams = new ImageUploadParams
      {
        File = new FileDescription(fileName, stream),
        Folder = folder,
        PublicId = Guid.NewGuid().ToString("N"),
        Overwrite = false,
        Transformation = new Transformation()
              .FetchFormat("webp")
              .Quality("auto:good")
      };

      _logger.LogInformation("Uploading {FileName} to Cloudinary folder: {Folder}", fileName, folder);

      // Tạo token riêng cho upload: 3 phút timeout, độc lập với request token
      using var uploadCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

      // Link với request token — nếu request cancel thì upload cũng cancel
      // Nhưng upload có timeout riêng 3 phút, không phụ thuộc Kestrel request timeout
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
          uploadCts.Token,
          cancellationToken
      );

      var result = await _cloudinary.UploadAsync(uploadParams, linkedCts.Token);

      if (result.Error != null)
      {
        _logger.LogError("Cloudinary upload lỗi: {Error}", result.Error.Message);
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);
      }

      _logger.LogInformation("Upload thành công: {Url}", result.SecureUrl);
      return result.SecureUrl.ToString();
    }

    public async Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
      var publicId = ExtractPublicId(fileUrl);

      _logger.LogInformation("Xóa file Cloudinary: {PublicId}", publicId);

      var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId));

      if (result.Error != null)
        _logger.LogError("Cloudinary delete lỗi: {Error}", result.Error.Message);
    }

    public async Task DeleteFolderAsync(string folder, CancellationToken cancellationToken = default)
    {
      _logger.LogInformation("Xóa folder Cloudinary: {Folder}", folder);

      // Xóa toàn bộ assets trong folder trước
      await _cloudinary.DeleteResourcesByPrefixAsync(folder);

      // Sau đó xóa folder rỗng
      await _cloudinary.DeleteFolderAsync(folder);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Trích xuất PublicId từ Cloudinary URL để dùng cho DeleteAsync.
    /// URL dạng: https://res.cloudinary.com/{cloud}/image/upload/v123/{folder}/{id}.webp
    /// PublicId:  {folder}/{id}
    /// </summary>
    private static string ExtractPublicId(string fileUrl)
    {
      var uri = new Uri(fileUrl);
      var path = uri.AbsolutePath;
      var uploadIndex = path.IndexOf("/upload/", StringComparison.Ordinal);
      var afterUpload = path[(uploadIndex + 8)..]; // bỏ "/upload/"

      // Bỏ version prefix (vd: "v1234567890/")
      var parts = afterUpload.Split('/');
      if (parts[0].StartsWith('v') && long.TryParse(parts[0][1..], out _))
        afterUpload = string.Join("/", parts[1..]);

      // Bỏ extension (.webp, .jpg...)
      return Path.ChangeExtension(afterUpload, null);
    }
  }
}
