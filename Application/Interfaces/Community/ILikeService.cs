using Application.DTOs.Community;

namespace Application.Interfaces.Community
{
  public interface ILikeService
  {
    Task<LikeResponse> ToggleAsync(
        int userId,
        LikeRequest request,
        CancellationToken cancellationToken = default
    );
  }
}
