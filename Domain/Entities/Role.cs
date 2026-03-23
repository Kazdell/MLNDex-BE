using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
  public class Role
  {
    public int RoleId { get; set; }
    public RoleName RoleName { get; set; }

    // Navigation
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
  }

  public enum RoleName
  {
    READER,
    CREATOR,
    TRANSLATOR,
    MODERATOR,
    ADMIN
  }
}
