namespace Application.DTOs.Creator
{
  public class CreatorRegisterDto
  {
    public string PenName { get; set; } = null!;
  }

  public class CreatorProfileDto
  {
    public int CreatorId { get; set; }
    public string PenName { get; set; } = null!;
    public string ModerationStatus { get; set; } = null!;
    public bool IsActive { get; set; }
  }

  public class UpdateUnlockSettingsDto
  {
    public bool UnlockEnabled { get; set; }
    public int? DefaultUnlockPriceCoins { get; set; }
    public bool FreeAfterEnabled { get; set; }
    public int? DefaultFreeAfterDays { get; set; }
  }

  public class CreatorRegisterResponseDto
  {
    public int CreatorId { get; set; }
    public string PenName { get; set; } = null!;
    public string ModerationStatus { get; set; } = null!;
    public bool IsActive { get; set; }
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
  }
}
