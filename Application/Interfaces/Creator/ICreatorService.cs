using Application.DTOs.Creator;

namespace Application.Interfaces.Creator
{
  public interface ICreatorService
  {
    Task<CreatorRegisterResponseDto> RegisterAsync(int userId, CreatorRegisterDto dto, CancellationToken ct = default);
    Task<UpdateUnlockSettingsDto> GetUnlockSettingsAsync(int userId, CancellationToken ct = default);
    Task<bool> UpdateUnlockSettingsAsync(int userId, UpdateUnlockSettingsDto dto, CancellationToken ct = default);
  }
}
