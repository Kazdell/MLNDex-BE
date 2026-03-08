using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Auth
{
	public class VerifyOtpDto
	{
		[Required, EmailAddress]
		public string Email { get; set; } = null!;

		[Required, StringLength(6, MinimumLength = 6)]
		public string Code { get; set; } = null!;
	}
}
