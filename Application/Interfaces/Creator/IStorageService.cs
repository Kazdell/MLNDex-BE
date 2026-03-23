using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Creator
{
  public interface IStorageService
  {
    /// Upload file, trả về URL public để lưu vào DB
    Task<string> UploadAsync(
        Stream stream,
        string fileName,
        string folder,
        CancellationToken cancellationToken = default);

    /// Xóa 1 file theo URL (khi xóa 1 trang đơn lẻ)
    Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default);

    /// Xóa toàn bộ folder (khi xóa cả chapter)
    Task DeleteFolderAsync(string folder, CancellationToken cancellationToken = default);
  }
}
