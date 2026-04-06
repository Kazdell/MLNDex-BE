namespace Application.DTOs.Payment;

/// <summary>
/// DTO chuẩn hoá sau khi Infrastructure verify và parse xong.
/// TopUpService chỉ làm việc với class này, không biết cổng cụ thể.
/// </summary>
public class PaymentCallbackDto
{
  /// <summary>Mã giao dịch nội bộ — khớp với TxnRef khi tạo đơn.</summary>
  public string TxnRef { get; set; } = string.Empty;

  /// <summary>Trạng thái chuẩn hoá: "PAID" | "CANCELLED" | "FAILED"</summary>
  public string Status { get; set; } = string.Empty;

  /// <summary>Số tiền thực tế cổng xác nhận (VND).</summary>
  public long AmountPaid { get; set; }

  /// <summary>Mã giao dịch phía cổng — lưu để đối soát.</summary>
  public string? GatewayTransactionId { get; set; }

  /// <summary>"PAYOS" | "VNPAY" | "MOMO" | "BANK_TRANSFER"</summary>
  public string Gateway { get; set; } = string.Empty;

  /// <summary>
  /// Chữ ký đã được Infrastructure verify.
  /// false → TopUpService reject ngay.
  /// </summary>
  public bool IsSignatureValid { get; set; }

  public DateTime TransactionTime { get; set; }
}

// ════════════════════════════════════════════════════════
// Raw webhook payloads — Infrastructure nhận từ cổng,
// parse rồi map vào PaymentCallbackDto ở trên.
// ════════════════════════════════════════════════════════

/// <summary>Raw webhook data từ payOS.</summary>
public class PayOsWebhookData
{
  public string Code { get; set; } = string.Empty;
  public string Desc { get; set; } = string.Empty;
  public bool Success { get; set; }
  public string Signature { get; set; } = string.Empty;

  // data object
  public long OrderCode { get; set; }
  public int Amount { get; set; }
  public string Description { get; set; } = string.Empty;
  public string AccountNumber { get; set; } = string.Empty;
  public string Reference { get; set; } = string.Empty;
  public string TransactionDateTime { get; set; } = string.Empty;
  public string Currency { get; set; } = string.Empty;
  public string PaymentLinkId { get; set; } = string.Empty;
  public string CounterAccountBankId { get; set; } = string.Empty;
  public string CounterAccountBankName { get; set; } = string.Empty;
  public string CounterAccountName { get; set; } = string.Empty;
  public string CounterAccountNumber { get; set; } = string.Empty;
  public string VirtualAccountName { get; set; } = string.Empty;
  public string VirtualAccountNumber { get; set; } = string.Empty;
}

/// <summary>
/// Raw IPN data từ VNPay.
/// VNPay gửi GET request với các params này về ReturnUrl và IpnUrl.
/// </summary>
public class VNPayCallbackDto
{
  public string vnp_TmnCode { get; set; } = string.Empty;
  public string vnp_Amount { get; set; } = string.Empty;
  public string vnp_BankCode { get; set; } = string.Empty;
  public string vnp_BankTranNo { get; set; } = string.Empty;
  public string vnp_CardType { get; set; } = string.Empty;
  public string vnp_PayDate { get; set; } = string.Empty;
  public string vnp_OrderInfo { get; set; } = string.Empty;
  public string vnp_TransactionNo { get; set; } = string.Empty;

  /// <summary>"00" = thành công, các code khác = thất bại.</summary>
  public string vnp_ResponseCode { get; set; } = string.Empty;
  public string vnp_TransactionStatus { get; set; } = string.Empty;

  /// <summary>TxnRef nội bộ — khớp với orderCode khi tạo đơn.</summary>
  public string vnp_TxnRef { get; set; } = string.Empty;
  public string vnp_SecureHash { get; set; } = string.Empty;
}

/// <summary>
/// Raw IPN data từ MoMo.
/// MoMo gửi POST JSON về IpnUrl.
/// </summary>
public class MoMoCallbackDto
{
  public string PartnerCode { get; set; } = string.Empty;
  public string OrderId { get; set; } = string.Empty;

  /// <summary>TxnRef nội bộ — khớp với requestId khi tạo đơn.</summary>
  public string RequestId { get; set; } = string.Empty;
  public long Amount { get; set; }
  public string OrderInfo { get; set; } = string.Empty;
  public string OrderType { get; set; } = string.Empty;
  public string TransId { get; set; } = string.Empty;

  /// <summary>0 = thành công, khác 0 = thất bại.</summary>
  public int ResultCode { get; set; }
  public string Message { get; set; } = string.Empty;
  public string PayType { get; set; } = string.Empty;
  public long ResponseTime { get; set; }
  public string ExtraData { get; set; } = string.Empty;
  public string Signature { get; set; } = string.Empty;
}
