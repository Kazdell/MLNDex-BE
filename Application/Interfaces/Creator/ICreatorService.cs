using Application.DTOs.Creator;

namespace Application.Interfaces.Creator
{
    public interface ICreatorService
    {
        Task<CreatorProfileDto> RegisterAsync(int userId, CreatorRegisterDto dto, CancellationToken ct = default);
    }
}
