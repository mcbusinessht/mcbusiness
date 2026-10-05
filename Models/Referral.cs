using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MCBusiness.Models
{
    public class Referral : BaseEntity
{
    [Required, ForeignKey("Referrer")]
    public string ReferrerId { get; set; } // Clé étrangère vers ApplicationUser (parrain)
    [Required, ForeignKey("Referred")]
    public string ReferredId { get; set; } // Clé étrangère vers ApplicationUser (filleul)
    [Required]
    public DateTimeOffset ReferredDate { get; set; } = DateTimeOffset.UtcNow; // Date du parrainage
    [Range(1, 10)]
    public int Generation { get; set; }
    
    // SUPPRIM�: BonusAmount et IsBonusLost 
    // Les points de parrainage sont maintenant gérés via PointTransaction
    // Cette table garde uniquement les relations pures entre utilisateurs

    // Navigation properties
    [ForeignKey("ReferrerId")]
    public ApplicationUser Referrer { get; set; }
    [ForeignKey("ReferredId")]
    public ApplicationUser Referred { get; set; }
}

}
