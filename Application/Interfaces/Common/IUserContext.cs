using System.Security.Claims;

namespace Application.Interfaces.Common
{
    public interface IUserContext
    {
        int? UserId { get; }
        string? Username { get; }
        string? Email { get; }
        bool IsAuthenticated { get; }
        IEnumerable<string> Roles { get; }
    }
}
