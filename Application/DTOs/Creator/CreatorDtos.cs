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
}
