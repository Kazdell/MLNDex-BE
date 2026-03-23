using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Payment
{
	/// <summary>Kết quả xử lý callback từ gateway (VNPay, MoMo, Bank Transfer).</summary>
	public class TopUpCallbackResponseDto
	{
		public string TxnRef { get; set; } = string.Empty;

		/// <summary>"success" | "failed" | "pending"</summary>
		public string Status { get; set; } = string.Empty;

		public long CoinsAdded { get; set; }
		public decimal NewBalance { get; set; }
		public string Message { get; set; } = string.Empty;
	}
}
