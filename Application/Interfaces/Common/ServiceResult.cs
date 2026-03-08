using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Common
{
	public class ServiceResult
	{
		public bool Success { get; set; }
		public string Message { get; set; } = null!;

		public static ServiceResult Ok(string message = "Thành công")
			=> new() { Success = true, Message = message };

		public static ServiceResult Fail(string message)
			=> new() { Success = false, Message = message };
	}
}
