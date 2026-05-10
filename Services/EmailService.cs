using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;

namespace Computer_networks.Services
{
    public static class EmailService
    {
        // #1 ИСПРАВЛЕНО: Данные берутся из App.config, не захардкожены
        private static string SenderEmail => ConfigurationManager.AppSettings["EmailSender"] ?? "";
        private static string SenderPassword => ConfigurationManager.AppSettings["EmailPassword"] ?? "";
        private static string SenderName => ConfigurationManager.AppSettings["EmailSenderName"] ?? "Электронный учебник";

        public static string GenerateCode()
        {
            // Используем криптографически стойкий генератор вместо Random
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                byte[] bytes = new byte[4];
                rng.GetBytes(bytes);
                int value = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 900000 + 100000;
                return value.ToString();
            }
        }

        public static string SendVerificationCode(string toEmail, string code)
        {
            try
            {
                // #5 ИСПРАВЛЕНО: убрано ServerCertificateValidationCallback = true (MITM-уязвимость)
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var message = new MailMessage
                {
                    From = new MailAddress(SenderEmail, SenderName),
                    Subject = "Подтверждение регистрации — Информационные технологии",
                    IsBodyHtml = true,
                    Body = BuildEmailBody(code)
                };
                message.To.Add(toEmail);

                using (var smtp = new SmtpClient("smtp.gmail.com", 587))
                {
                    smtp.EnableSsl = true;
                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtp.UseDefaultCredentials = false;
                    smtp.Credentials = new NetworkCredential(SenderEmail, SenderPassword);
                    smtp.Timeout = 30000;
                    smtp.Send(message);
                }

                System.Diagnostics.Debug.WriteLine($"[EmailService] Код отправлен на {toEmail}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmailService] ОШИБКА: {ex.Message}");
                return ex.Message;
            }
        }

        private static string BuildEmailBody(string code)
        {
            return $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; background: #f5f5f5; padding: 30px;'>
  <div style='max-width: 480px; margin: 0 auto; background: white; border-radius: 12px;
              padding: 40px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);'>
    <h2 style='color: #1565C0; margin-top: 0;'>Электронный учебник: Информационные технологии</h2>
    <p style='color: #333; font-size: 16px;'>Для завершения регистрации введи код подтверждения:</p>
    <div style='background: #E3F2FD; border-radius: 8px; padding: 20px; text-align: center; margin: 24px 0;'>
      <span style='font-size: 36px; font-weight: bold; letter-spacing: 8px; color: #1565C0;'>{code}</span>
    </div>
    <p style='color: #666; font-size: 14px;'>Код действителен <strong>15 минут</strong>.</p>
    <p style='color: #999; font-size: 12px; margin-bottom: 0;'>
      Если ты не регистрировался — просто проигнорируй это письмо.
    </p>
  </div>
</body>
</html>";
        }
    }
}