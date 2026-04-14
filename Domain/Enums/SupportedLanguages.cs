namespace Domain.Enums
{
  /// <summary>
  /// Centralized list of supported languages.
  /// This is the Single Source of Truth for language validation across the entire system.
  /// Frontend fetches this list via GET /api/languages.
  /// Backend validates incoming language values against this list.
  /// </summary>
  public static class SupportedLanguages
  {
    public static readonly LanguageInfo[] All = new[]
    {
            new LanguageInfo(1, "vi", "Tiếng Việt"),
            new LanguageInfo(2, "en", "Tiếng Anh"),
            new LanguageInfo(3, "ja", "Tiếng Nhật"),
            new LanguageInfo(4, "ko", "Tiếng Hàn"),
            new LanguageInfo(5, "zh", "Tiếng Trung"),
            new LanguageInfo(6, "fr", "Tiếng Pháp"),
        };

    /// <summary>
    /// Check if a language code or display name is valid.
    /// Accepts both code ("vi") and display name ("Tiếng Việt").
    /// </summary>
    public static bool IsValid(string language)
    {
      if (string.IsNullOrWhiteSpace(language)) return false;

      return All.Any(l =>
          l.Code.Equals(language, StringComparison.OrdinalIgnoreCase) ||
          l.Name.Equals(language, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get language info by code or name. Returns null if not found.
    /// </summary>
    public static LanguageInfo? GetByCodeOrName(string language)
    {
      if (string.IsNullOrWhiteSpace(language)) return null;

      return All.FirstOrDefault(l =>
          l.Code.Equals(language, StringComparison.OrdinalIgnoreCase) ||
          l.Name.Equals(language, StringComparison.OrdinalIgnoreCase));
    }
  }

  public class LanguageInfo
  {
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }

    public LanguageInfo(int id, string code, string name)
    {
      Id = id;
      Code = code;
      Name = name;
    }
  }
}
