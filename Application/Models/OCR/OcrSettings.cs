namespace Application.Models.OCR
{
  public class OcrSettings
  {
    /// <summary>
    /// Ngưỡng xác suất để nhận dạng đó là 1 điểm Text thật sự (TextThreshold). 
    /// Nhỏ hơn = chấp nhận cả nét cực mờ.
    /// Lớn hơn = chỉ giữ nét đậm rõ ràng.
    /// Mặc định: 0.45f
    /// </summary>
    public float TextThreshold { get; set; } = 0.45f;

    /// <summary>
    /// Kích thước Kernel (Dilation/MorphClose) để liên kết các nét rời rạc thành từng cụm từ/câu.
    /// Quá thấp = dễ bị cắt đôi các từ rời rạc.
    /// Quá cao = dễ bị nối nhầm các khối chữ xa nhau.
    /// Mặc định chuẩn Manga: 15
    /// </summary>
    public int LinkKernelSize { get; set; } = 15;

    /// <summary>
    /// Khoảng dãn viền an toàn (Padding) tính bằng Pixel xung quanh vùng Bounding Box gốc tìm được.
    /// Thêm padding để cắt chữ không lẹm mất nét ngoài cùng.
    /// Mặc định: 5
    /// </summary>
    public int BoxPadding { get; set; } = 5;

    /// <summary>
    /// Chế độ tiền xử lý ảnh để phân tách chữ và nền.
    /// Giá trị hỗ trợ: "Adaptive" (Cũ - gây rỗng chữ to), "Otsu" (Mới - giữ nét rất tốt cho CJK Box).
    /// Mặc định: "Otsu"
    /// </summary>
    public string BinarizationMode { get; set; } = "Otsu";
  }
}
