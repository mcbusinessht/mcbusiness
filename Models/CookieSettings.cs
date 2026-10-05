using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Models
{
    public class CookieSettings
    {
        public bool Essential { get; set; } = true; // Toujours activé
        public bool Functional { get; set; } = false;
        public bool Analytics { get; set; } = false;
        public bool Marketing { get; set; } = false;
        public bool Performance { get; set; } = false;
        public DateTimeOffset ConsentDate { get; set; } = DateTimeOffset.UtcNow;
        public string ConsentVersion { get; set; } = "1.0";
        public string UserAgent { get; set; }
        public string IpAddress { get; set; }
    }

    public class CookieConsentRecord
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string SessionId { get; set; }
        public bool Essential { get; set; } = true;
        public bool Functional { get; set; }
        public bool Analytics { get; set; }
        public bool Marketing { get; set; }
        public bool Performance { get; set; }
        public DateTimeOffset ConsentDate { get; set; }
        public string ConsentVersion { get; set; }
        public string UserAgent { get; set; }
        public string IpAddress { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        public ApplicationUser User { get; set; }
    }

    public enum CookieCategory
    {
        Essential,
        Functional,
        Analytics,
        Marketing,
        Performance
    }

    public class CookieInfo
    {
        public string Name { get; set; }
        public string Purpose { get; set; }
        public string Duration { get; set; }
        public CookieCategory Category { get; set; }
        public string Provider { get; set; }
        public bool IsThirdParty { get; set; }
        public string DataProcessing { get; set; }
    }
}
