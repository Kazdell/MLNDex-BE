using Application.DTOs.Creator;
using Application.DTOs.UserList;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces.Services
{
    public interface IUserListService
    {
        Task<UserListDto> CreateListAsync(int userId, CreateUserListDto request);
        Task<UserListDto> UpdateListAsync(int userId, int listId, UpdateUserListDto request);
        Task DeleteListAsync(int userId, int listId);
        
        Task<PaginatedList<UserListDto>> GetUserListsAsync(int userId, int page = 1, int pageSize = 20);
        Task<PaginatedList<UserListDto>> GetPublicListsAsync(int page = 1, int pageSize = 20);
        
        Task<UserListDetailDto> GetListDetailAsync(int listId, int? currentUserId);
        
        Task<UserListItemDto> AddItemToListAsync(int userId, int listId, AddUserListItemDto request);
        Task RemoveItemFromListAsync(int userId, int listId, int seriesId);
    }
}
