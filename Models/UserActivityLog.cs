using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCBusiness.Models
{
    public class UserActivityLog
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string Path { get; set; }
        public string Method { get; set; }
        public string IpAddress { get; set; }
        public int? ProductId { get; set; }  // Nouveau : identifie le produit cliqué
        public string ActionType { get; set; }  // Nouveau : "Click", "AddToCart", etc.
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public string? UserAgent { get; set; }  // Nouveau : pour stocker le User-Agent du navigateur
        public string? Referrer { get; set; }  // Nouveau : pour stocker l'URL de la page précédente
    }
}
