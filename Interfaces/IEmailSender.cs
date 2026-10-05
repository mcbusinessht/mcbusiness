using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCBusiness.Interfaces
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
        
         // Ajout de la méthode pour envoyer une page HTML
        Task SendEmailWithHtmlFileAsync(string toEmail, string subject, string htmlFilePath);
    
    }
}
