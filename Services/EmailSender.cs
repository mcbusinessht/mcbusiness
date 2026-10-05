using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using MCBusiness.Interfaces;
using MCBusiness.Models;
using Microsoft.Extensions.Options;

namespace MCBusiness.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly EmailSettings _emailSettings;

        public EmailSender(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings?.Value ?? throw new ArgumentNullException(nameof(emailSettings));

            // Validation des paramètres � la construction
            ValidateEmailSettings(_emailSettings);
        }

        private void ValidateEmailSettings(EmailSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.FromEmail))
                throw new InvalidOperationException("Le champ 'FromEmail' est requis.");
            if (string.IsNullOrWhiteSpace(settings.SmtpServer))
                throw new InvalidOperationException("Le champ 'SmtpServer' est requis.");
            if (settings.SmtpPort <= 0)
                throw new InvalidOperationException("Le champ 'SmtpPort' doit être un entier positif.");
            if (string.IsNullOrWhiteSpace(settings.SmtpUsername))
                throw new InvalidOperationException("Le champ 'SmtpUsername' est requis.");
            if (string.IsNullOrWhiteSpace(settings.SmtpPassword))
                throw new InvalidOperationException("Le champ 'SmtpPassword' est requis.");
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            try
            {
                // Configuration de l'email
                var fromAddress = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName);
                var toAddress = new MailAddress(toEmail);

                using (var smtp = new SmtpClient
                {
                    Host = _emailSettings.SmtpServer,
                    Port = _emailSettings.SmtpPort,
                    EnableSsl = _emailSettings.UseSSL,
                    Credentials = new NetworkCredential(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword)
                })
                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                })
                {
                    await smtp.SendMailAsync(message);
                }
            }
            catch (SmtpException smtpEx)
            {
                // Gestion spécifique des erreurs SMTP
                throw new InvalidOperationException($"Erreur SMTP : {smtpEx.Message}", smtpEx);
            }
            catch (Exception ex)
            {
                // Gestion des erreurs générales
                throw new InvalidOperationException($"Erreur lors de l'envoi de l'email : {ex.Message}", ex);
            }
        }
        public async Task SendEmailWithHtmlFileAsync(string toEmail, string subject, string htmlFilePath)
        {
            try
            {
                // Charger le contenu HTML du fichier
                string htmlMessage = await File.ReadAllTextAsync(htmlFilePath);

                // Configuration de l'email
                var fromAddress = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName);
                var toAddress = new MailAddress(toEmail);

                using (var smtp = new SmtpClient
                {
                    Host = _emailSettings.SmtpServer,
                    Port = _emailSettings.SmtpPort,
                    EnableSsl = _emailSettings.UseSSL,
                    Credentials = new NetworkCredential(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword)
                })
                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                })
                {
                    await smtp.SendMailAsync(message);
                }
            }
            catch (SmtpException smtpEx)
            {
                throw new InvalidOperationException($"Erreur SMTP : {smtpEx.Message}", smtpEx);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Erreur lors de l'envoi de l'email : {ex.Message}", ex);
            }
        }

    }
}
