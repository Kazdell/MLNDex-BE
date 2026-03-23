using Application.DTOs.Community;
using Application.Interfaces.Community;
using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Community
{
  public class LikeService : ILikeService
  {
    private readonly IMlndexDbContext _context;

    public LikeService(IMlndexDbContext context)
    {
      _context = context;
    }

    public async Task<LikeResponse> ToggleAsync(
        int userId,
        LikeRequest request,
        CancellationToken cancellationToken = default
    )
    {
      var existing = await _context.Likes.FirstOrDefaultAsync(
          l =>
              l.UserId == userId
              && l.TargetId == request.TargetId
              && l.TargetType == request.TargetType,
          cancellationToken
      );

      if (existing != null)
      {
        _context.Likes.Remove(existing);
      }
      else
      {
        _context.Likes.Add(
            new Like
            {
              UserId = userId,
              TargetId = request.TargetId,
              TargetType = request.TargetType,
              CreatedAt = DateTime.UtcNow,
            }
        );
      }

      await _context.SaveChangesAsync(cancellationToken);

      var total = await _context.Likes.CountAsync(
          l => l.TargetId == request.TargetId && l.TargetType == request.TargetType,
          cancellationToken
      );

      return new LikeResponse { Liked = existing == null, TotalLikes = total };
    }
  }
}
