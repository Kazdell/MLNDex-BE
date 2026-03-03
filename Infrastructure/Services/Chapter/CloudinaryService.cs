using Application.Interfaces.Chapter;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services.Chapter
{
	/// Implement IStorageService dùng Cloudinary.
	/// Tự động convert ảnh sang WebP và tối ưu chất lượng.
	public class CloudinaryService : IStorageService
	{
		private readonly Cloudinary _cloudinary;
		private readonly ILogger<CloudinaryService> _logger;

		public CloudinaryService(IConfiguration config, ILogger<CloudinaryService> logger)
		{
			_logger = logger;

			var cloudName = config["Cloudinary:CloudName"]
				?? throw new InvalidOperationException("Thiếu Cloudinary:CloudName trong appsettings");
			var apiKey = config["Cloudinary:ApiKey"]
				?? throw new InvalidOperationException("Thiếu Cloudinary:ApiKey trong appsettings");
			var apiSecret = config["Cloudinary:ApiSecret"]
				?? throw new InvalidOperationException("Thiếu Cloudinary:ApiSecret trong appsettings");

			var account = new Account(cloudName, apiKey, apiSecret);
			_cloudinary = new Cloudinary(account)
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
				Folder = folder,                        // vd: "chapters/5/pages"
				PublicId = Guid.NewGuid().ToString("N"), // tên file unique, tránh trùng
				Overwrite = false,
				// Tự động convert sang WebP + tối ưu quality → ảnh nhẹ hơn ~40%
				Transformation = new Transformation()
					.FetchFormat("webp")
					.Quality("auto:good")
			};

			_logger.LogInformation("Uploading {FileName} to Cloudinary folder: {Folder}", fileName, folder);

			var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken);

			if (result.Error != null)
			{
				_logger.LogError("Cloudinary upload lỗi: {Error}", result.Error.Message);
				throw new InvalidOperationException($"Upload thất bại: {result.Error.Message}");
			}

			_logger.LogInformation("Upload thành công: {Url}", result.SecureUrl);

			// Trả về URL public để lưu vào DB (Chapter_Page.image_url)
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
