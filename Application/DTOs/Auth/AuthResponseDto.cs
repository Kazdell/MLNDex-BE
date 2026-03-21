using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Auth
{
	public class AuthResponseDto
	{
		public string AccessToken { get; set; } = null!;
		public int UserId { get; set; }
		public DateTime ExpiresAt { get; set; }
		public string Username { get; set; } = null!;
		public string DisplayName { get; set; } = null!;
		public string Email { get; set; } = null!;
		public List<string> Roles { get; set; } = new();
		public string RefreshToken { get; set; } = null!;
		public bool CannotUpload { get; set; }
	}
}
