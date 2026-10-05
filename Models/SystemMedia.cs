using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Models
{
    public class SystemMedia
    {
        [Key] 
        public int Id{get;set;}
        public string Name{get;set;}
        public string Alt{get;set;}
        public string SizeInKb{get;set;}
        public string Path{get;set;}
        public DateTimeOffset Date {get;set;}=DateTimeOffset.UtcNow; 
        public bool IsDeleted { get; set; } = false;
    }
}
