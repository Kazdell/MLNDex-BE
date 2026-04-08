using Application.DTOs.Common;
using System.Collections.Generic;

namespace Application.Exceptions
{
  public static class ErrorMessages
  {
    private static readonly Dictionary<string, string> _messages = new()
        {
            { ErrorCodes.INTERNAL_SERVER_ERROR, "Đã xảy ra lỗi không xác định." },
            { ErrorCodes.BAD_REQUEST, "Yêu cầu không hợp lệ." },
            { ErrorCodes.UNAUTHORIZED, "Không có quyền truy cập." },
            { ErrorCodes.FORBIDDEN, "Bị từ chối truy cập." },
            { ErrorCodes.NOT_FOUND, "Không tìm thấy dữ liệu." },
            { ErrorCodes.USER_NOT_FOUND, "Người dùng không tồn tại." },
            { ErrorCodes.COIN_PACKAGE_NOT_FOUND, "Gói coin không tồn tại." },
            { ErrorCodes.SERIES_NOT_FOUND, "Truyện không tồn tại." },
            { ErrorCodes.SYSTEM_CONFIG_NOT_FOUND, "Chưa có cấu hình hệ thống nào được thiết lập." },

            { ErrorCodes.DUPLICATE_TRANSLATION_TEAM, "Nhóm dịch đã đăng một bản dịch ngôn ngữ này cho chương gốc." },
            { ErrorCodes.TRANSLATION_NOT_FOUND, "Bản dịch không tồn tại." },

            { ErrorCodes.INSUFFICIENT_BALANCE, "Không đủ số dư để thực hiện giao dịch." },
            { ErrorCodes.CHAPTER_ALREADY_UNLOCKED, "Chương này đã được mở khóa từ trước." },
            { ErrorCodes.CHAPTER_LOCKED, "Chương này chưa được mở khóa." },

            { ErrorCodes.OPERATION_NOT_ALLOWED, "Thao tác không được phép." },

            { ErrorCodes.WALLET_NOT_FOUND, "Ví không tồn tại." },
            { ErrorCodes.SUBSCRIPTION_NOT_FOUND, "Gói đăng ký không tồn tại." },
            { ErrorCodes.VIP_PACKAGE_NOT_FOUND, "Gói VIP không tồn tại." },
            { ErrorCodes.INVALID_TRANSACTION, "Giao dịch không hợp lệ hoặc đã được xử lý." }
        };

    public static string GetMessage(string code)
    {
      return _messages.TryGetValue(code, out var message) ? message : _messages[ErrorCodes.INTERNAL_SERVER_ERROR];
    }
  }
}
