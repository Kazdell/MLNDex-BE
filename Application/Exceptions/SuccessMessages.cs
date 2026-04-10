using Application.DTOs.Common;
using System.Collections.Generic;

namespace Application.Exceptions
{
  public static class SuccessMessages
  {
    private static readonly Dictionary<string, string> _messages = new()
    {
      { SuccessCodes.REGISTRATION_SUCCESS, "Đăng ký thành công. Vui lòng kiểm tra email để xác thực." },
      { SuccessCodes.EMAIL_VERIFIED, "Xác thực email thành công. Bạn có thể đăng nhập." },
      { SuccessCodes.LOGOUT_SUCCESS, "Đăng xuất thành công." },
      { SuccessCodes.OTP_SENT, "Nếu email tồn tại, chúng tôi đã gửi mã xác minh." },
      { SuccessCodes.PASSWORD_RESET_SUCCESS, "Đặt lại mật khẩu thành công. Vui lòng đăng nhập." },
      { SuccessCodes.PASSWORD_CHANGED, "Đổi mật khẩu thành công." }
    };

    public static string GetMessage(string code)
    {
      if (_messages.TryGetValue(code, out var message))
      {
        return message;
      }
      return "Thao tác thành công.";
    }
  }
}
