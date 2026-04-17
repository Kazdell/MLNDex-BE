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
            { ErrorCodes.USER_ALREADY_EXISTS, "Thông tin (Email hoặc Username) đã được sử dụng." },
            { ErrorCodes.INVALID_CREDENTIALS, "Mật khẩu hoặc thông tin đăng nhập không chính xác." },
            { ErrorCodes.INVALID_TOKEN, "Mã OTP hoặc Token không hợp lệ hoặc đã hết hạn." },
            { ErrorCodes.TOKEN_EXPIRED, "Phiên đăng nhập đã hết hạn." },
            { ErrorCodes.COIN_PACKAGE_NOT_FOUND, "Gói coin không tồn tại." },
            { ErrorCodes.SERIES_NOT_FOUND, "Truyện không tồn tại." },
            { ErrorCodes.SYSTEM_CONFIG_NOT_FOUND, "Chưa có cấu hình hệ thống nào được thiết lập." },

            { ErrorCodes.DUPLICATE_TRANSLATION_TEAM, "Nhóm dịch đã đăng một bản dịch ngôn ngữ này cho chương gốc." },
            { ErrorCodes.TRANSLATION_NOT_FOUND, "Bản dịch không tồn tại." },
            { ErrorCodes.DUPLICATE_TRANSLATION_LANGUAGE, "Đã tồn tại bản dịch ngôn ngữ này cho chương truyện. Vui lòng chọn ngôn ngữ khác." },

            { ErrorCodes.INSUFFICIENT_BALANCE, "Không đủ số dư để thực hiện giao dịch." },
            { ErrorCodes.CHAPTER_ALREADY_UNLOCKED, "Chương này đã được mở khóa từ trước." },
            { ErrorCodes.CHAPTER_LOCKED, "Chương này chưa được mở khóa." },

            { ErrorCodes.OPERATION_NOT_ALLOWED, "Thao tác không được phép." },

            { ErrorCodes.WALLET_NOT_FOUND, "Ví không tồn tại." },
            { ErrorCodes.SUBSCRIPTION_NOT_FOUND, "Gói đăng ký không tồn tại." },
            { ErrorCodes.VIP_PACKAGE_NOT_FOUND, "Gói VIP không tồn tại." },
            { ErrorCodes.INVALID_TRANSACTION, "Giao dịch không hợp lệ hoặc đã được xử lý." },

            { ErrorCodes.TEAM_NOT_FOUND, "Nhóm dịch không tồn tại." },
            { ErrorCodes.CHAPTER_NOT_FOUND, "Chương truyện không tồn tại." },
            { ErrorCodes.NOT_TEAM_MEMBER, "Bạn không phải là thành viên hoặc trưởng nhóm của nhóm dịch này." },
            { ErrorCodes.TRANSLATION_PERMISSION_NOT_FOUND, "Không tìm thấy thông tin phân quyền dịch giả." },
            { ErrorCodes.LANGUAGE_MISMATCH, "Ngôn ngữ được chọn không khớp với yêu cầu phân quyền gốc." },
            { ErrorCodes.PERMISSION_NOT_VALID_FOR_SERIES, "Bản quyền dịch thuật không khớp với tựa truyện này." },
            { ErrorCodes.TEAM_ID_REQUIRED_UNOFFICIAL, "Bắt buộc phải cung cấp thông tin nhóm cho bản dịch tự do." },
            { ErrorCodes.TRANSLATION_RETRIEVE_FAILED, "Lỗi xẩy ra khi tải dữ liệu bản dịch." },
            { ErrorCodes.MISSING_TRANSLATION_PERMISSION, "Lỗi dữ liệu: Ghi nhận phân quyền dịch bị khuyết thiếu." },
            { ErrorCodes.UNAUTHORIZED_EDIT, "Bạn không có quyền chỉnh sửa bản dịch này." },
            { ErrorCodes.UNAUTHORIZED_DELETE, "Bạn không có quyền xóa bản dịch này." },
            { ErrorCodes.TRANSLATION_NOT_FOUND_OR_NOT_OWNER, "Không tìm thấy bản dịch hoặc bản dịch không thuộc thẩm quyền của bạn." },

            { ErrorCodes.CHAPTER_PRICE_NOT_CONFIGURED, "Chương này chưa được cấu hình giá mở khóa." },
            { ErrorCodes.INVALID_TRANSLATION_CHAPTER, "Bản dịch không liên kết với chương hợp lệ." },
            { ErrorCodes.ORIGINAL_CHAPTER_FREE, "Chương gốc đang miễn phí, bản dịch này không cần mua." },
            { ErrorCodes.ORIGINAL_CHAPTER_UNLOCKED, "Bạn đã mở khóa chương gốc nên có thể đọc tất cả bản dịch miễn phí." },
            { ErrorCodes.TRANSLATION_ALREADY_UNLOCKED, "Bạn đã mua bản dịch này rồi." },
            { ErrorCodes.TEAM_MONETIZATION_DISABLED, "Nhóm dịch này chưa bật tính năng kinh doanh." },
            { ErrorCodes.TRANSLATION_PRICE_NOT_CONFIGURED, "Bản dịch này chưa được cấu hình giá mở khóa." },
            { ErrorCodes.INVALID_UNLOCK_PRICE, "Giá mở khóa không hợp lệ." },
            
            { ErrorCodes.LANGUAGE_NOT_FOUND, "Ngôn ngữ không tồn tại." },
            { ErrorCodes.LANGUAGE_ALREADY_TRANSLATED, "Ngôn ngữ này đã có nhóm dịch chính thức được cấp quyền. Không thể chấp nhận thêm." },
            { ErrorCodes.PERMISSION_REVOKED, "Quyền dịch chính thức đã bị thu hồi, không thể đăng tải dưới dạng Official." },
            { ErrorCodes.PERMISSION_REQUEST_PENDING, "Yêu cầu dịch truyện của nhóm cho bộ này đang chờ xử lý." },
            { ErrorCodes.PERMISSION_ALREADY_GRANTED, "Nhóm đã có quyền dịch chính thức cho bộ truyện này." },
            { ErrorCodes.PERMISSION_REQUEST_NOT_FOUND, "Không tìm thấy yêu cầu phân quyền dịch." },
            { ErrorCodes.CREATOR_ONLY_REVIEW, "Chỉ tính danh tác giả của bộ truyện mới được quyền duyệt phân quyền này." },
            { ErrorCodes.TEAM_MEMBER_ONLY_VIEW, "Bạn phải là thành viên nhóm dịch để xem các yêu cầu này." },
            { ErrorCodes.DUPLICATE_TEAM_SLUG, "Mã rút gọn (Slug) của nhóm đã tồn tại." },
            { ErrorCodes.TEAM_NOT_FOUND_OR_UNAUTHORIZED, "Không tìm thấy nhóm hoặc bạn không có thẩm quyền với nhóm này." },
            { ErrorCodes.USER_ALREADY_IN_TEAM, "Thành viên này đã có sẵn trong nhóm." },
            { ErrorCodes.INVITATION_ALREADY_PENDING, "Đã có lời mời đang chờ xử lý gửi đến người dùng này." },
            { ErrorCodes.CANNOT_REMOVE_LEADER, "Không thể xóa Trưởng nhóm khỏi đội." },
            { ErrorCodes.LEADER_CANNOT_LEAVE_TEAM, "Trưởng nhóm không thể rời nhóm. Vui lòng giải tán nhóm hoặc chuyển quyền trước." },
            { ErrorCodes.CANNOT_CHANGE_LEADER_ROLE, "Chức danh Trưởng nhóm không thể thay đổi thông qua thao tác này." },
            { ErrorCodes.LEADERSHIP_TRANSFER_REQUIRES_INVITATION, "Việc chuyển giao chức vụ Trưởng nhóm yêu cầu người được chọn phải Chấp Nhận lời mời. Hãy dùng tính năng Mời thành viên." },
            { ErrorCodes.TEAM_JOIN_COOLDOWN, "Bạn phải đợi một khoảng thời gian sau khi rời nhóm trước khi tham gia mới." },
            { ErrorCodes.MAX_TEAMS_REACHED, "Mỗi người dùng chỉ được tham gia tối đa 5 nhóm dịch cùng lúc." },
            
            { ErrorCodes.CHAPTER_NOT_PUBLISHED, "Chương này chưa được phát hành." },
            { ErrorCodes.CHAPTER_ALREADY_FREE, "Chương này đã miễn phí, không cần mở khóa." },
            { ErrorCodes.CREATOR_NOT_FOUND, "Không tìm thấy hồ sơ tác giả." },
            { ErrorCodes.PEN_NAME_TAKEN, "Bút danh này đã được sử dụng. Vui lòng chọn bút danh khác." },
            
            { ErrorCodes.COMMENT_NOT_FOUND, "Bình luận không tồn tại." },
            { ErrorCodes.COMMENT_MAX_DEPTH_REACHED, "Không thể phản hồi một bình luận đã là phản hồi (chỉ hỗ trợ 2 cấp độ bình luận)." }
        };

    public static string GetMessage(string code)
    {
      return _messages.TryGetValue(code, out var message) ? message : _messages[ErrorCodes.INTERNAL_SERVER_ERROR];
    }
  }
}
