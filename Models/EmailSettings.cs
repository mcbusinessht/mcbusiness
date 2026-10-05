using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


    namespace MCBusiness.Models
{
    public class EmailSettings
    {
        public string FromEmail { get; set; }  // L'email d'expéditeur
        public string FromName { get; set; }   // Le nom de l'expéditeur
        public string SmtpServer { get; set; } // Le serveur SMTP
        public int SmtpPort { get; set; }      // Le port SMTP (souvent 587 pour TLS ou 465 pour SSL)
        public string SmtpUsername { get; set; } // Le nom d'utilisateur SMTP (souvent votre adresse email)
        public string SmtpPassword { get; set; } // Le mot de passe SMTP (ou le mot de passe de votre compte email)
        public bool UseSSL { get; set; }      // Utilisation de SSL
    }
}

