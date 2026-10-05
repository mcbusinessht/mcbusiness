using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class Employee
    {
        [Key]
        public int Id { get; set; }

        [Required, ForeignKey("User")]
        public string UserId { get; set; }
        public virtual ApplicationUser User { get; set; }

        [Required, ForeignKey("Post")]
        public int PostId { get; set; }
        public virtual Post Post { get; set; }

        [Required]
        public decimal Salary { get; set; } = 0;

        public decimal? SalarySnapshot { get; set; }

        [ForeignKey("Currency")]
        public int CurrencyBase { get; set; } = 1;
        public virtual Currency Currency { get; set; }

        public DateTimeOffset HiredAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? ContractEndDate { get; set; } // Pour CDD/CDI

        public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

        public bool IsDeleted { get; set; } = false;
        public DateTimeOffset? UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public enum EmployeeStatus
    {
        Active,
        Suspended,
        Terminated,
        Resigned
    }
}
