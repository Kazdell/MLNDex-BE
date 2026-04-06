using Application.DTOs.Creator;
using Application.DTOs.UserList;
using Application.Interfaces.Data;
using Application.Interfaces.Services;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
  public class UserListService : IUserListService
  {
    private readonly IMlndexDbContext _context;

    public UserListService(IMlndexDbContext context)
    {
      _context = context;
    }

    public async Task<UserListDto> CreateListAsync(int userId, CreateUserListDto request)
    {
      var userList = new UserList
      {
        UserId = userId,
        Name = request.Name,
        Description = request.Description,
        IsPublic = request.IsPublic,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
      };

      _context.UserLists.Add(userList);
      await _context.SaveChangesAsync();

      var creator = await _context.CreatorProfiles.FirstOrDefaultAsync(c => c.UserId == userId);
      var username = await _context.Users.Where(u => u.UserId == userId).Select(u => u.Username).FirstOrDefaultAsync();

      return new UserListDto
      {
        UserListId = userList.UserListId,
        UserId = userList.UserId,
        CreatorName = creator?.PenName ?? username ?? "Anonymous",
        Name = userList.Name,
        Description = userList.Description,
        CreatedAt = userList.CreatedAt,
        UpdatedAt = userList.UpdatedAt,
        IsPublic = userList.IsPublic,
        ItemCount = 0
      };
    }

    public async Task<UserListDto> UpdateListAsync(int userId, int listId, UpdateUserListDto request)
    {
      var userList = await _context.UserLists
          .Include(ul => ul.Items)
          .Include(ul => ul.User)
              .ThenInclude(u => u.CreatorProfile)
          .FirstOrDefaultAsync(ul => ul.UserListId == listId);

      if (userList == null)
      {
        throw new KeyNotFoundException("Không tìm thấy danh sách");
      }

      if (userList.UserId != userId)
      {
        throw new UnauthorizedAccessException("Bạn không có quyền sửa danh sách này");
      }

      userList.Name = request.Name;
      userList.Description = request.Description;
      userList.IsPublic = request.IsPublic;
      userList.UpdatedAt = DateTime.UtcNow;

      await _context.SaveChangesAsync();

      return new UserListDto
      {
        UserListId = userList.UserListId,
        UserId = userList.UserId,
        CreatorName = userList.User.CreatorProfile?.PenName ?? userList.User.Username,
        Name = userList.Name,
        Description = userList.Description,
        CreatedAt = userList.CreatedAt,
        UpdatedAt = userList.UpdatedAt,
        IsPublic = userList.IsPublic,
        ItemCount = userList.Items.Count
      };
    }

    public async Task DeleteListAsync(int userId, int listId)
    {
      var userList = await _context.UserLists
          .Include(ul => ul.Items)
          .FirstOrDefaultAsync(ul => ul.UserListId == listId);

      if (userList == null)
      {
        throw new KeyNotFoundException("Không tìm thấy danh sách");
      }

      if (userList.UserId != userId)
      {
        throw new UnauthorizedAccessException("Bạn không có quyền xóa danh sách này");
      }

      _context.UserListItems.RemoveRange(userList.Items);
      _context.UserLists.Remove(userList);

      await _context.SaveChangesAsync();
    }

    public async Task<PaginatedList<UserListDto>> GetUserListsAsync(int userId, int page = 1, int pageSize = 20)
    {
      var query = _context.UserLists
          .Include(ul => ul.User)
              .ThenInclude(u => u.CreatorProfile)
          .Where(ul => ul.UserId == userId);

      var totalCount = await query.CountAsync();

      var lists = await query
          .OrderByDescending(ul => ul.UpdatedAt)
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .Select(ul => new UserListDto
          {
            UserListId = ul.UserListId,
            UserId = ul.UserId,
            CreatorName = ul.User.CreatorProfile != null ? ul.User.CreatorProfile.PenName : ul.User.Username,
            Name = ul.Name,
            Description = ul.Description,
            CreatedAt = ul.CreatedAt,
            UpdatedAt = ul.UpdatedAt,
            IsPublic = ul.IsPublic,
            ItemCount = ul.Items.Count
          })
          .ToListAsync();

      return new PaginatedList<UserListDto>
      {
        Items = lists,
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize
      };
    }

    public async Task<PaginatedList<UserListDto>> GetPublicListsAsync(int page = 1, int pageSize = 20)
    {
      var query = _context.UserLists
          .Include(ul => ul.User)
              .ThenInclude(u => u.CreatorProfile)
          .Where(ul => ul.IsPublic);

      var totalCount = await query.CountAsync();

      var lists = await query
          .OrderByDescending(ul => ul.UpdatedAt)
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .Select(ul => new UserListDto
          {
            UserListId = ul.UserListId,
            UserId = ul.UserId,
            CreatorName = ul.User.CreatorProfile != null ? ul.User.CreatorProfile.PenName : ul.User.Username,
            Name = ul.Name,
            Description = ul.Description,
            CreatedAt = ul.CreatedAt,
            UpdatedAt = ul.UpdatedAt,
            IsPublic = ul.IsPublic,
            ItemCount = ul.Items.Count
          })
          .ToListAsync();

      return new PaginatedList<UserListDto>
      {
        Items = lists,
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize
      };
    }

    public async Task<UserListDetailDto> GetListDetailAsync(int listId, int? currentUserId)
    {
      var userList = await _context.UserLists
          .Include(ul => ul.User)
              .ThenInclude(u => u.CreatorProfile)
          .Include(ul => ul.Items)
              .ThenInclude(i => i.Series)
          .FirstOrDefaultAsync(ul => ul.UserListId == listId);

      if (userList == null)
      {
        throw new KeyNotFoundException("Không tìm thấy danh sách");
      }

      if (!userList.IsPublic && userList.UserId != currentUserId)
      {
        throw new UnauthorizedAccessException("Danh sách này đang ở chế độ riêng tư");
      }

      return new UserListDetailDto
      {
        UserListId = userList.UserListId,
        UserId = userList.UserId,
        CreatorName = userList.User.CreatorProfile?.PenName ?? userList.User.Username,
        Name = userList.Name,
        Description = userList.Description,
        CreatedAt = userList.CreatedAt,
        UpdatedAt = userList.UpdatedAt,
        IsPublic = userList.IsPublic,
        ItemCount = userList.Items.Count,
        Items = userList.Items.OrderByDescending(i => i.AddedAt).Select(i => new UserListItemDto
        {
          UserListItemId = i.UserListItemId,
          UserListId = i.UserListId,
          SeriesId = i.SeriesId,
          AddedAt = i.AddedAt,
          SeriesTitle = i.Series.Title,
          SeriesCoverUrl = i.Series.CoverImageUrl,
          SeriesStatus = i.Series.Status.ToString(),
          SeriesFormat = i.Series.SeriesFormat.ToString()
        }).ToList()
      };
    }

    public async Task<UserListItemDto> AddItemToListAsync(int userId, int listId, AddUserListItemDto request)
    {
      var userList = await _context.UserLists
          .FirstOrDefaultAsync(ul => ul.UserListId == listId);

      if (userList == null)
      {
        throw new KeyNotFoundException("Không tìm thấy danh sách");
      }

      if (userList.UserId != userId)
      {
        throw new UnauthorizedAccessException("Bạn không có quyền sửa danh sách này");
      }

      var series = await _context.Series.FirstOrDefaultAsync(s => s.SeriesId == request.SeriesId);
      if (series == null)
      {
        throw new KeyNotFoundException("Không tìm thấy truyện");
      }

      var existingItem = await _context.UserListItems
          .FirstOrDefaultAsync(i => i.UserListId == listId && i.SeriesId == request.SeriesId);

      if (existingItem != null)
      {
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED, "Truyện này đã có trong danh sách");
      }

      var item = new UserListItem
      {
        UserListId = listId,
        SeriesId = request.SeriesId,
        AddedAt = DateTime.UtcNow
      };

      _context.UserListItems.Add(item);
      userList.UpdatedAt = DateTime.UtcNow;

      await _context.SaveChangesAsync();

      return new UserListItemDto
      {
        UserListItemId = item.UserListItemId,
        UserListId = item.UserListId,
        SeriesId = item.SeriesId,
        AddedAt = item.AddedAt,
        SeriesTitle = series.Title,
        SeriesCoverUrl = series.CoverImageUrl,
        SeriesStatus = series.Status.ToString(),
        SeriesFormat = series.SeriesFormat.ToString()
      };
    }

    public async Task RemoveItemFromListAsync(int userId, int listId, int seriesId)
    {
      var userList = await _context.UserLists
          .FirstOrDefaultAsync(ul => ul.UserListId == listId);

      if (userList == null)
      {
        throw new KeyNotFoundException("Không tìm thấy danh sách");
      }

      if (userList.UserId != userId)
      {
        throw new UnauthorizedAccessException("Bạn không có quyền sửa danh sách này");
      }

      var item = await _context.UserListItems
          .FirstOrDefaultAsync(i => i.UserListId == listId && i.SeriesId == seriesId);

      if (item == null)
      {
        throw new KeyNotFoundException("Không tìm thấy truyện trong danh sách");
      }

      _context.UserListItems.Remove(item);
      userList.UpdatedAt = DateTime.UtcNow;

      await _context.SaveChangesAsync();
    }
  }
}
