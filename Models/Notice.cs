using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
#nullable enable

namespace MCBusiness.Models
{
    public class Notice
    {
        public int Id { get; set; }
        public string Name {get; set;}
        public string Content {get; set; }
        public NoticeType Type { get; set; }
        public DateTimeOffset EndDate {get; set;}
        public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow;
        public bool IsDeleted { get; set; } = false;
        public string? UrlLogo { get; set; }
        public string? UrlLink { get; set; } = null;
    }
    public enum NoticeType{

        Announcement,
        TopBar,
        Modal,
        Publicity
    }
}
