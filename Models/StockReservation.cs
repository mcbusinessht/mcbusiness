using System.ComponentModel.DataAnnotations;

namespace MCBusiness.Models
{
    public class StockReservation
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int ProductVariantId { get; set; }
        
        [Required]
        public string UserId { get; set; }
        
        [Required]
        public int Quantity { get; set; }
        
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? ConfirmedAt { get; set; }
        public DateTimeOffset? ReleasedAt { get; set; }
        
        public ReservationStatus Status { get; set; }

        // Navigation properties
        public virtual ProductVariant ProductVariant { get; set; }
        public virtual ApplicationUser User { get; set; }
    }

    public class BalanceReservation
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; }
        public WalletType WalletType { get; set; } = WalletType.Customer;
        public int? WalletId { get; set; }
        
        [Required]
        public decimal Amount { get; set; }
        
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? ConfirmedAt { get; set; }
        public DateTimeOffset? ReleasedAt { get; set; }
        
        public ReservationStatus Status { get; set; }

        public virtual ApplicationUser User { get; set; }
    }

    public enum ReservationStatus
    {
        Active,
        Confirmed,
        Released,
        Expired
    }
}
