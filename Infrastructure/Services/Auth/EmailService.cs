using Application.Interfaces.Auth;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services.Auth
{
  public class EmailService : IEmailService
  {
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
      _config = config;
    }

    public async Task SendOtpEmailAsync(string toEmail, string otpCode)
    {
      var message = new MimeMessage();
      message.From.Add(new MailboxAddress(
          _config["EmailSettings:SenderName"] ?? "MLNDex",
          _config["EmailSettings:SenderEmail"] ?? "no-reply@mlndex.com"));
      message.To.Add(MailboxAddress.Parse(toEmail));
      message.Subject = "Mã xác thực tài khoản";

      message.Body = new TextPart("html")
      {
        Text = $"""
                    <div style="font-family: Arial, sans-serif; max-width: 400px; margin: auto;">
                        <h2>Xác thực tài khoản</h2>
                        <p>Mã OTP của bạn là:</p>
                        <h1 style="letter-spacing: 8px; color: #4F46E5;">{otpCode}</h1>
                        <p>Mã có hiệu lực trong <strong>10 phút</strong>.</p>
                        <p>Nếu bạn không yêu cầu mã này, hãy bỏ qua email này.</p>
                    </div>
                    """
      };

      using var smtp = new SmtpClient();
      await smtp.ConnectAsync(
          _config["EmailSettings:SmtpHost"],
          int.Parse(_config["EmailSettings:SmtpPort"]!),
          SecureSocketOptions.StartTls);

      await smtp.AuthenticateAsync(
          _config["EmailSettings:SenderEmail"] ?? "",
          _config["EmailSettings:Password"] ?? "");

      await smtp.SendAsync(message);
      await smtp.DisconnectAsync(true);
    }
  }
}
