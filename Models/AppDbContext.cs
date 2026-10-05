using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Models
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;
        public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor? httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<AmbassadorPlan> AmbassadorPlans { get; set; }
        public DbSet<AmbassadorSubscription> AmbassadorSubscriptions { get; set; }
        public DbSet<Referral> Referrals { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<SystemMedia> SystemMedias { get; set; }
        public DbSet<Newsletter> Newsletters { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderProduct> OrderProducts { get; set; }
        public DbSet<Shipment> Shipments { get; set; }
        public DbSet<ProductReview> ProductReviews { get; set; }
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Characteristic> Characteristics { get; set; }
        public DbSet<CharacteristicValue> CharacteristicValues { get; set; }
        public DbSet<ProductVariantCharacteristic> ProductVariantCharacteristics { get; set; }
        public DbSet<Company> Companies {get;  set; }
        public DbSet<ProductVariant> ProductVariants { get; set; }
        public DbSet<PriceBreak> PriceBreaks { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<UserActivityLog> UserActivityLogs { get; set; }
        public DbSet<CookieConsentRecord> CookieConsentRecords { get; set; }
        
        // Tables de configuration système pour paramètres dynamiques
        public DbSet<SystemConfiguration> SystemConfiguration { get; set; }
        public DbSet<SystemConfigurationHistory> SystemConfigurationHistory { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<Notice> Notices { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<ReturnRequest> ReturnRequests{ get; set; }
        public DbSet<DeliveryPerson> DeliveryPersons{ get; set; }
        public DbSet<Country> Countries{ get; set; }
        public DbSet<State> States{ get; set; }
        public DbSet<City> Cities{ get; set; }
        public DbSet<Neighborhood> Neighborhoods{ get; set; }
        public DbSet<ShippingFee> ShippingFees{ get; set; }
        public DbSet<SellerPayment> SellerPayments{ get; set; }
        public DbSet<PromoCode> PromoCodes{ get; set; }
        public DbSet<PromoCodeUsage> PromoCodeUsages{ get; set; }
        public DbSet<WalletTransaction> WalletTransactions{ get; set; }
        public DbSet<Post> Posts{ get; set; } 
        public DbSet<Employee> Employees { get; set; }
        public DbSet<PlatformTransaction> PlatformTransactions { get; set; }
        public DbSet<Expense> Expenses { get; set; } 
        public DbSet<BankTransferPayment> BankTransferPayments { get; set; } 
        public DbSet<SearchHistory> SearchHistories { get; set; } 
        public DbSet<PromoCampaign> PromoCampaigns { get; set; } 
        public DbSet<AmbassadorPlanBonus> AmbassadorPlanBonuses { get; set; } 
        public DbSet<AmbassadorPlanBonusMapping> AmbassadorPlanBonusMappings { get; set; }
        public DbSet<Commission> Commissions { get; set; }
        public DbSet<AmbassadorCommission> AmbassadorCommissions { get; set; }
        public DbSet<PointConversion> PointConversions { get; set; }
        public DbSet<PointEvent> PointEvents { get; set; }
        public DbSet<UserPoint> UserPoints { get; set; }
        public DbSet<CompanySector> CompanySectors { get; set; }
        public DbSet<StockReservation> StockReservations { get; set; }
        public DbSet<BalanceReservation> BalanceReservations { get; set; }
        public DbSet<EmployeeTask> EmployeeTasks { get; set; }
        public DbSet<EmployeePerformance> EmployeePerformances { get; set; }
        public DbSet<EmployeeAttendance> EmployeeAttendances { get; set; }
        
        // NOUVEAU: Système de points de fidélité
        public DbSet<PointTransaction> PointTransactions { get; set; }
        
        // NOUVEAU: Candidature vendeur
        public DbSet<SellerApplication> SellerApplications { get; set; }






//SupplierPayment

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // (A) Contrainte anti doublon: (AmbassadorId, OrderId, CompanyId, Type)
            // Une commission unique par ambassadeur/commande/entreprise/type
            builder.Entity<AmbassadorCommission>()
                .HasIndex(c => new { c.AmbassadorId, c.OrderId, c.CompanyId, c.Type })
                .IsUnique();

            // (C) Devise: FK vers Currency
            builder.Entity<AmbassadorCommission>()
                .HasOne(c => c.Currency)
                .WithMany()
                .HasForeignKey(c => c.CurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            // ============================================================
            // CONTRAINTES CASCADE SÉCURISÉES - Éviter les cycles CASCADE
            // ============================================================
            
            // Configuration des relations avec DELETE NO ACTION pour éviter les cycles
            builder.Entity<OrderProduct>()
                .HasOne(op => op.Order)
                .WithMany(o => o.Products)
                .HasForeignKey(op => op.OrderId)
                .OnDelete(DeleteBehavior.Restrict); // RESTRICT au lieu de CASCADE pour éviter cycles

            builder.Entity<Payment>()
                .HasOne(p => p.Order)
                .WithOne(o => o.Payment)
                .HasForeignKey<Payment>(p => p.OrderId)
                .OnDelete(DeleteBehavior.SetNull); // SET NULL pour paiements

            builder.Entity<Shipment>()
                .HasOne(s => s.OrderProduct)
                .WithOne(op => op.Shipment)
                .HasForeignKey<Shipment>(s => s.OrderProductId)
                .OnDelete(DeleteBehavior.SetNull); // SET NULL pour expéditions

            // Protection des utilisateurs - RESTRICT pour données critiques
            builder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Ne jamais supprimer utilisateur avec commandes

            builder.Entity<Transaction>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Ne jamais supprimer utilisateur avec transactions

            // Relations produits - CASCADE approprié
            builder.Entity<ProductVariant>()
                .HasOne(pv => pv.Product)
                .WithMany(p => p.Variants)
                .HasForeignKey(pv => pv.ProductId)
                .OnDelete(DeleteBehavior.Cascade); // Supprimer variantes avec produit

            builder.Entity<ProductImage>()
                .HasOne(pi => pi.ProductVariant)
                .WithMany(pv => pv.Images)
                .HasForeignKey(pi => pi.ProductVariantId)
                .OnDelete(DeleteBehavior.Cascade); // Supprimer images avec variante

            // Configuration des types decimal
            builder.Entity<AmbassadorPlan>().Property(p => p.Ceiling).HasColumnType("decimal(18,2)");
            builder.Entity<AmbassadorPlan>().Property(p => p.Price).HasColumnType("decimal(18,2)");
            builder.Entity<ProductVariant>().Property(p => p.Price).HasColumnType("decimal(18,2)");
            builder.Entity<ProductVariant>().Property(p => p.WeightKg).HasColumnType("decimal(18,2)");
            builder.Entity<ShippingFee>().Property(p => p.Price).HasColumnType("decimal(18,2)");
            builder.Entity<ShippingFee>().Property(p => p.BasePrice).HasColumnType("decimal(18,2)");
            builder.Entity<ShippingFee>().Property(p => p.AdditionalItemPrice).HasColumnType("decimal(18,2)");
            builder.Entity<ShippingFee>().Property(p => p.AdditionalUnitPrice).HasColumnType("decimal(18,2)");
            builder.Entity<ShippingFee>().Property(p => p.PricePerKg).HasColumnType("decimal(18,2)");
            builder.Entity<ShippingFee>().Property(p => p.MinPrice).HasColumnType("decimal(18,2)");
            builder.Entity<ShippingFee>().Property(p => p.MaxPrice).HasColumnType("decimal(18,2)");
            // SUPPRIMÉ: BonusAmount property from Referral
            builder.Entity<Transaction>().Property(t => t.Amount).HasColumnType("decimal(18,2)");
            builder.Entity<Wallet>().Property(w => w.Balance).HasColumnType("decimal(18,2)");
            // SIMPLIFIÉ: Un seul pourcentage de commission ambassadeur
            builder.Entity<AmbassadorPlan>().Property(p => p.AmbassadorCommissionPercentage).HasColumnType("decimal(18,2)");
            
            // NOUVEAU: Configuration des champs de points
            builder.Entity<AmbassadorPlan>().Property(p => p.PointsGeneration1).HasDefaultValue(0);
            builder.Entity<AmbassadorPlan>().Property(p => p.PointsGeneration2).HasDefaultValue(0);
            builder.Entity<AmbassadorPlan>().Property(p => p.PointsGeneration3).HasDefaultValue(0);
            builder.Entity<AmbassadorPlan>().Property(p => p.WelcomeBonusPoints).HasDefaultValue(0);
            builder.Entity<AmbassadorPlan>().Property(p => p.MonthlyPointsLimit).HasDefaultValue(0);
            builder.Entity<AmbassadorPlan>().Property(p => p.IsFreeplan).HasDefaultValue(false);
            builder.Entity<ApplicationUser>().Property(u => u.Balance).HasColumnType("decimal(18,2)");
            builder.Entity<Payment>().Property(p => p.Amount).HasColumnType("decimal(18,2)");
            builder.Entity<Currency>().Property(p => p.RateToBase).HasColumnType("decimal(18,5)");
            builder.Entity<OrderProduct>().Property(p => p.PriceAtOrder).HasColumnType("decimal(18,2)");
            builder.Entity<ProductVariant>().Property(p => p.PriceSnapshot).HasColumnType("decimal(18,2)");
            builder.Entity<Employee>().Property(p => p.Salary).HasColumnType("decimal(18,2)");
            builder.Entity<Employee>().Property(p => p.SalarySnapshot).HasColumnType("decimal(18,5)");
            builder.Entity<OrderProduct>().Property(p => p.DeliveryFee).HasColumnType("decimal(18,2)");
            builder.Entity<WalletTransaction>().Property(t => t.Balance).HasColumnType("decimal(18,2)");
            builder.Entity<BankTransferPayment>().Property(t => t.Amount).HasColumnType("decimal(18,2)");
            builder.Entity<Commission>().Property(t => t.Amount).HasColumnType("decimal(18,2)");
            builder.Entity<AmbassadorPlanBonus>().Property(t => t.BonusAmount).HasColumnType("decimal(18,2)");
            builder.Entity<PointConversion>().Property(t => t.Amount).HasColumnType("decimal(18,2)");

            // SellerApplication
            builder.Entity<SellerApplication>().HasIndex(sa => sa.Email);
            builder.Entity<SellerApplication>().Property(sa => sa.Status).HasConversion<string>();

            // Enregistrement des enums comme chaînes
            builder.Entity<AmbassadorSubscription>().Property(s => s.Status).HasConversion<string>();
            builder.Entity<Transaction>().Property(t => t.Type).HasConversion<string>();
            builder.Entity<Transaction>().Property(t => t.WalletType).HasConversion<string>();
            builder.Entity<Transaction>().Property(t => t.Status).HasConversion<string>();
            builder.Entity<Transaction>().Property(t => t.Method).HasConversion<string>();
            builder.Entity<Shipment>().Property(t => t.Status).HasConversion<string>();
            builder.Entity<Payment>().Property(t => t.PaymentMethod).HasConversion<string>();
            builder.Entity<Category>().Property(t => t.Type).HasConversion<string>();
            builder.Entity<ProductVariant>().Property(t => t.ShippingPackageType).HasConversion<string>();
            builder.Entity<ShippingFee>().Property(t => t.PackageType).HasConversion<string>();
            builder.Entity<Order>().Property(t => t.Status).HasConversion<string>();
            builder.Entity<OrderProduct>().Property(t => t.Status).HasConversion<string>();
            builder.Entity<ApplicationUser>().Property(t => t.PreferredLanguage).HasConversion<string>();
            builder.Entity<WalletTransaction>().Property(t => t.Type).HasConversion<string>();
            builder.Entity<Wallet>().Property(t => t.Type).HasConversion<string>();
            builder.Entity<WalletTransaction>().Property(t => t.WalletType).HasConversion<string>();
            builder.Entity<BankTransferPayment>().Property(t => t.WalletType).HasConversion<string>();
            builder.Entity<BalanceReservation>().Property(t => t.WalletType).HasConversion<string>();
            builder.Entity<PlatformTransaction>().Property(t => t.Type).HasConversion<string>();
            builder.Entity<PlatformTransaction>().Property(t => t.Category).HasConversion<string>();
            builder.Entity<Expense>().Property(t => t.Category).HasConversion<string>();
            builder.Entity<Employee>().Property(t => t.Status).HasConversion<string>();
            builder.Entity<BankTransferPayment>().Property(t => t.TransferMethod).HasConversion<string>();
            builder.Entity<BankTransferPayment>().Property(t => t.Direction).HasConversion<string>();
            builder.Entity<BankTransferPayment>().Property(t => t.Status).HasConversion<string>();
            builder.Entity<PointEvent>().Property(t => t.EventType).HasConversion<string>();
            builder.Entity<PointConversion>().Property(t => t.ConversionType).HasConversion<string>();
            

            //BankTransferPayment
            builder.Entity<BankTransferPayment>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BankTransferPayment>()
                .HasOne(r => r.Transaction)
                .WithMany()
                .HasForeignKey(r => r.TransactionId)
                .OnDelete(DeleteBehavior.Restrict);


            // Configuration des relations de clés étrangères
            builder.Entity<Referral>()
                .HasOne(r => r.Referrer)
                .WithMany()
                .HasForeignKey(r => r.ReferrerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Referral>()
                .HasOne(r => r.Referred)
                .WithMany()
                .HasForeignKey(r => r.ReferredId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Transaction>()
                .HasOne(r => r.Currency)
                .WithMany()
                .HasForeignKey(r => r.CurrencyBase)
                .OnDelete(DeleteBehavior.Restrict);

            // Conversation
            builder.Entity<Conversation>()
                .HasOne(r => r.User1)
                .WithMany()
                .HasForeignKey(r => r.User1Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Conversation>()
                .HasOne(r => r.User2)
                .WithMany()
                .HasForeignKey(r => r.User2Id)
                .OnDelete(DeleteBehavior.Restrict);


            // Plan
            builder.Entity<AmbassadorPlan>()
                .HasOne(p => p.Category)
                .WithMany()
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // //Company
            // builder.Entity<Company>()
            //     .HasOne(p => p.Category)
            //     .WithMany()
            //     .HasForeignKey(p => p.Sector)
            //     .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Shipment>()
                .HasOne(l => l.OrderProduct)
                .WithOne(c => c.Shipment)
                .HasForeignKey<Shipment>(l => l.OrderProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApplicationUser>()
                .HasOne(l => l.PreferredCurrency)
                .WithMany()
                .HasForeignKey(l => l.PreferredCurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Product
            builder.Entity<Product>().HasKey(p => p.Id);
            builder.Entity<Product>().Property(p => p.Name).IsRequired().HasMaxLength(100);
            builder.Entity<Product>().HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId);

            // Category
            builder.Entity<Category>().HasKey(c => c.Id);
            builder.Entity<Category>().Property(c => c.Name).IsRequired().HasMaxLength(100);
            builder.Entity<Category>().HasOne(c => c.ParentCategory).WithMany(c => c.SubCategories).HasForeignKey(c => c.ParentCategoryId).IsRequired(false);

            // Product Images
            builder.Entity<ProductImage>().HasKey(pi => pi.Id);
            builder.Entity<ProductImage>().Property(pi => pi.Url).IsRequired().HasMaxLength(200);
            builder.Entity<ProductImage>().HasOne(pi => pi.ProductVariant).WithMany(pv => pv.Images).HasForeignKey(pi => pi.ProductVariantId);


            // Characteristic
            builder.Entity<Characteristic>().HasKey(c => c.Id);
            builder.Entity<Characteristic>().Property(c => c.Name).IsRequired().HasMaxLength(50);

            // CharacteristicValue
            builder.Entity<CharacteristicValue>().HasKey(cv => cv.Id);
            builder.Entity<CharacteristicValue>().Property(cv => cv.Value).IsRequired().HasMaxLength(100);
            builder.Entity<CharacteristicValue>().HasOne(cv => cv.Characteristic).WithMany(c => c.Values).HasForeignKey(cv => cv.CharacteristicId);

            // ProductVariant
            builder.Entity<ProductVariant>().HasKey(pv => pv.Id);
            builder.Entity<ProductVariant>().Property(pv => pv.Sku).IsRequired().HasMaxLength(50);
            builder.Entity<ProductVariant>().Property(pv => pv.Price).IsRequired().HasColumnType("decimal(18,2)");
            builder.Entity<ProductVariant>().HasOne(pv => pv.Product).WithMany(p => p.Variants).HasForeignKey(pv => pv.ProductId);
            builder.Entity<ProductVariant>().HasOne(pv => pv.Currency).WithMany().HasForeignKey(pv => pv.CurrencyBase);

            // PriceBreak
            builder.Entity<PriceBreak>().HasKey(pb => pb.Id);
            builder.Entity<PriceBreak>().Property(pb => pb.UnitPrice).IsRequired().HasColumnType("decimal(18,2)");
            builder.Entity<PriceBreak>().Property(pb => pb.DiscountPercentage).HasColumnType("decimal(5,2)");
            builder.Entity<PriceBreak>().HasOne(pb => pb.ProductVariant).WithMany(pv => pv.PriceBreaks).HasForeignKey(pb => pb.ProductVariantId).OnDelete(DeleteBehavior.Cascade);
            // ProductVariantCharacteristic

            // WalletTransaction - Configuration de base (pas de relation vers Transaction)
            builder.Entity<WalletTransaction>().HasKey(wt => wt.Id);

            // Stock
            builder.Entity<Stock>().HasKey(s => s.Id);
            builder.Entity<Stock>().HasOne(s => s.ProductVariant).WithOne(pv => pv.Stock).HasForeignKey<Stock>(s => s.ProductVariantId);

            // Cart
            builder.Entity<Cart>().HasKey(c => c.Id);

            builder.Entity<CartItem>()
                .HasOne(ci => ci.Cart)
                .WithMany(c => c.Items)
                .HasForeignKey(ci => ci.CartId)
                .IsRequired();

            builder.Entity<CartItem>()
                .HasOne(ci => ci.ProductVariant)
                .WithMany()
                .HasForeignKey(ci => ci.ProductVariantId)
                .IsRequired();

            // CartItem
            builder.Entity<CartItem>().HasKey(ci => ci.Id);


            builder.Entity<ProductVariantCharacteristic>()
                .HasKey(pvc => new { pvc.ProductVariantId, pvc.CharacteristicId, pvc.CharacteristicValueId });

            //Relation ProduitVariantCharacteristique
            builder.Entity<ProductVariantCharacteristic>()
                .HasOne(pvc => pvc.ProductVariant)
                .WithMany(p => p.ProductVariantCharacteristics)
                .HasForeignKey(pvc => pvc.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);  // Au lieu de Cascade

            builder.Entity<ProductVariantCharacteristic>()
                .HasOne(pvc => pvc.Characteristic)
                .WithMany(c => c.ProductVariantCharacteristics)
                .HasForeignKey(pvc => pvc.CharacteristicId)
                .OnDelete(DeleteBehavior.Restrict);  // Au lieu de Cascade

            builder.Entity<ProductVariantCharacteristic>()
                .HasOne(pvc => pvc.CharacteristicValue)
                .WithMany(cv => cv.ProductVariantCharacteristics)
                .HasForeignKey(pvc => pvc.CharacteristicValueId)
                .OnDelete(DeleteBehavior.Restrict);  // Au lieu de Cascade

            // Relation avec Company
            builder.Entity<Product>()
                .HasOne(p => p.Company)
                .WithMany()
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            //Order
            builder.Entity<Order>()
                .HasOne(p => p.Currency)
                .WithMany()
                .HasForeignKey(p => p.CurrencyBase)
                .OnDelete(DeleteBehavior.Restrict);


            //OrderProduct
            builder.Entity<OrderProduct>()
                .HasOne(op => op.Currency)
                .WithMany()
                .HasForeignKey(op => op.CurrencyBase)
                .OnDelete(DeleteBehavior.Restrict);

            //ShippingFee
            builder.Entity<ShippingFee>()
                .HasOne(r => r.Company)
                .WithMany()
                .HasForeignKey(r => r.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ShippingFee>()
                .HasOne(r => r.Country)
                .WithMany()
                .HasForeignKey(r => r.CountryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ShippingFee>()
                .HasOne(r => r.State)
                .WithMany()
                .HasForeignKey(r => r.StateId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ShippingFee>()
                .HasOne(r => r.City)
                .WithMany()
                .HasForeignKey(r => r.CityId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ShippingFee>()
                .HasOne(r => r.Neighborhood)
                .WithMany()
                .HasForeignKey(r => r.NeighborhoodId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ShippingFee>()
                .HasOne(r => r.Currency)
                .WithMany()
                .HasForeignKey(r => r.CurrencyBase)
                .OnDelete(DeleteBehavior.Restrict);

            //Address
            builder.Entity<Address>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Address>()
                .HasOne(r => r.Country)
                .WithMany()
                .HasForeignKey(r => r.CountryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Address>()
                .HasOne(r => r.State)
                .WithMany()
                .HasForeignKey(r => r.StateId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Address>()
                .HasOne(r => r.City)
                .WithMany()
                .HasForeignKey(r => r.CityId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Address>()
                .HasOne(r => r.Neighborhood)
                .WithMany()
                .HasForeignKey(r => r.NeighborhoodId)
                .OnDelete(DeleteBehavior.Restrict);

            //SellerPayment
            builder.Entity<SellerPayment>()
                .HasOne(r => r.Order)
                .WithMany()
                .HasForeignKey(r => r.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            //Payment
            // 1–à–1 Payment ?? Order
            builder.Entity<Payment>()
                .HasOne(p => p.Order)
                .WithOne(o => o.Payment)
                .HasForeignKey<Payment>(p => p.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            // 1–à–1 Payment ?? Subscription
            builder.Entity<Payment>()
                .HasOne(p => p.AmbassadorSubscription)
                .WithOne(s => s.Payment)
                .HasForeignKey<Payment>(p => p.AmbassadorSubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .HasOne(r => r.Currency)
                .WithMany()
                .HasForeignKey(r => r.CurrencyBase)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .HasOne(p => p.PromoCodeUsage)
                .WithOne(s => s.Payment)
                .HasForeignKey<Payment>(p => p.PromoCodeUsageId)
                .OnDelete(DeleteBehavior.Restrict);

            //PromoCode
            builder.Entity<PromoCode>()
                .HasOne(r => r.CreatedByUser)
                .WithMany()
                .HasForeignKey(r => r.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PromoCode>()
                .HasOne(c => c.PromoCampaign)
                .WithMany(pc => pc.PromoCodes)
                .HasForeignKey(c => c.PromoCampaignId)
                .OnDelete(DeleteBehavior.Restrict);


            //PromoCodeUsage

            builder.Entity<PromoCodeUsage>()
                .HasOne(u => u.PromoCode)
                .WithMany(c => c.Usages)
                .HasForeignKey(u => u.PromoCodeId)
                .OnDelete(DeleteBehavior.Restrict);


            builder.Entity<PromoCodeUsage>()
                .HasOne(r => r.UsedByUser)
                .WithMany()
                .HasForeignKey(r => r.UsedByUserId)
                .OnDelete(DeleteBehavior.Restrict);


            //SellerPayment
            builder.Entity<SellerPayment>()
                .HasOne(r => r.Company)
                .WithMany()
                .HasForeignKey(r => r.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);


            builder.Entity<SellerPayment>()
                .HasOne(r => r.Order)
                .WithMany()
                .HasForeignKey(r => r.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SellerPayment>()
                .HasOne(r => r.Currency)
                .WithMany()
                .HasForeignKey(r => r.CurrencyBase)
                .OnDelete(DeleteBehavior.Restrict);

            // CONFIGURATION CRITIQUE MANQUANTE - Nouveaux champs SellerPayment
            builder.Entity<SellerPayment>().Property(sp => sp.GrossAmount).HasColumnType("decimal(18,2)");
            builder.Entity<SellerPayment>().Property(sp => sp.PlatformCommissionTotal).HasColumnType("decimal(18,2)");

            //WalletTransaction
            builder.Entity<WalletTransaction>()
                .HasOne(wt => wt.User)
                .WithMany()
                .HasForeignKey(wt => wt.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<WalletTransaction>()
                .HasOne(wt => wt.Currency)
                .WithMany()
                .HasForeignKey(wt => wt.CurrencyBase)
                .OnDelete(DeleteBehavior.Restrict);

            //Employee
            builder.Entity<Employee>()
                .HasOne(wt => wt.User)
                .WithMany()
                .HasForeignKey(wt => wt.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Employee>()
                .HasOne(wt => wt.Post)
                .WithMany()
                .HasForeignKey(wt => wt.PostId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Employee>()
                .HasOne(wt => wt.Currency)
                .WithMany()
                .HasForeignKey(wt => wt.CurrencyBase)
                .OnDelete(DeleteBehavior.Restrict);

            //PlatformTransaction
            builder.Entity<PlatformTransaction>()
                .HasOne(pt => pt.Order)
                .WithMany()
                .HasForeignKey(pt => pt.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PlatformTransaction>()
                .HasOne(pt => pt.AmbassadorSubscription)
                .WithMany()
                .HasForeignKey(pt => pt.AmbassadorSubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PlatformTransaction>()
                .HasOne(pt => pt.Seller)
                .WithMany()
                .HasForeignKey(pt => pt.SellerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PlatformTransaction>()
                .HasOne(pt => pt.Employee)
                .WithMany()
                .HasForeignKey(pt => pt.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            //Expense
            builder.Entity<Expense>()
                .HasOne(pt => pt.ApprovedByUser)
                .WithMany()
                .HasForeignKey(pt => pt.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Expense>()
                .HasOne(pt => pt.PlatformTransaction)
                .WithMany()
                .HasForeignKey(pt => pt.PlatformTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Expense>()
                .HasOne(pt => pt.ReceiverUser)
                .WithMany()
                .HasForeignKey(pt => pt.ReceiverUserId)
                .OnDelete(DeleteBehavior.Restrict);

            //AmbassadorPlanBonusMapping
            builder.Entity<AmbassadorPlanBonusMapping>()
                .HasOne(m => m.AmbassadorPlan)
                .WithMany()
                .HasForeignKey(m => m.AmbassadorPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AmbassadorPlanBonusMapping>()
                .HasOne(m => m.PlanBonus)
                .WithMany()
                .HasForeignKey(m => m.AmbassadorPlanBonusId)
                .OnDelete(DeleteBehavior.Restrict);

            //Commission
            builder.Entity<Commission>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Commission>()
                .HasOne(c => c.Order)
                .WithMany()
                .HasForeignKey(c => c.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            //PointConversion
            builder.Entity<PointConversion>()
                .HasOne(pc => pc.User)
                .WithMany()
                .HasForeignKey(pc => pc.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            builder.Entity<PointConversion>()
                .HasOne(pc => pc.Currency)
                .WithMany()
                .HasForeignKey(pc => pc.CurrencyBase)
                .OnDelete(DeleteBehavior.Restrict);

            //PointEvent
            builder.Entity<PointEvent>()
                .HasOne(pe => pe.User)
                .WithMany()
                .HasForeignKey(pe => pe.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            //UserPoint
            builder.Entity<UserPoint>()
                .HasOne(up => up.User)
                .WithMany()
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.Restrict);

             // Configuration de la relation parent-enfant
            builder.Entity<Category>()
                .HasOne(c => c.ParentCategory)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            //CompanySector
            // Configuration pour CompanySector
            builder.Entity<CompanySector>(entity =>
            {
                entity.HasIndex(cs => new { cs.CompanyId, cs.CategoryId })
                    .IsUnique();
                
                entity.HasOne(cs => cs.Company)
                    .WithMany(c => c.CompanySectors)
                    .HasForeignKey(cs => cs.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(cs => cs.Category)
                    .WithMany(c => c.CompanySectors)
                    .HasForeignKey(cs => cs.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuration pour Address
            builder.Entity<Address>(entity =>
            {
                // Contrainte : une adresse doit être liée soit à un User, soit à une Company
                entity.HasCheckConstraint(
                    "CK_Address_UserOrCompany", 
                    "\"UserId\" IS NOT NULL OR \"CompanyId\" IS NOT NULL");

                // Index pour les performances
                entity.HasIndex(a => new { a.UserId, a.IsDefault, a.IsDeleted });
                entity.HasIndex(a => new { a.CompanyId, a.IsDefault, a.IsDeleted });
                
                // Relation avec Company
                entity.HasOne(a => a.Company)
                    .WithMany(c => c.Addresses)
                    .HasForeignKey(a => a.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Ajout des INDEXES pour optimiser les performances
            builder.Entity<Referral>().HasIndex(r => new { r.ReferrerId, r.ReferredId }).HasDatabaseName("idx_referrals_referrer_referred");
            builder.Entity<AmbassadorSubscription>().HasIndex(s => s.UserId).HasDatabaseName("idx_subscriptions_userid");
            builder.Entity<AmbassadorSubscription>().HasIndex(s => s.Status).HasDatabaseName("idx_subscriptions_Status");
            builder.Entity<Wallet>()
                .HasIndex(w => new { w.UserId, w.CurrencyBase, w.Type })
                .IsUnique()
                .HasDatabaseName("ux_wallets_user_currency_type");
            builder.Entity<ApplicationUser>().HasIndex(u => u.Email).HasDatabaseName("idx_users_email");
            builder.Entity<ApplicationUser>().HasIndex(u => u.UserName).HasDatabaseName("idx_users_username");
            builder.Entity<ApplicationUser>().HasIndex(u => u.ReferralCode).HasDatabaseName("idx_users_referralcode");
            builder.Entity<ProductVariant>().HasIndex(p => p.Price).HasDatabaseName("idx_Product_Price");
            builder.Entity<Product>().HasIndex(p => p.CategoryId).HasDatabaseName("idx_Product_Category");
            builder.Entity<Product>().HasIndex(p => p.CompanyId).HasDatabaseName("idx_Product_CompanyId");
            builder.Entity<Company>().HasIndex(p => p.SellerId).HasDatabaseName("idx_Company_SellerId");
            builder.Entity<Order>().HasIndex(c => c.OrderDate).HasDatabaseName("idx_Order_date");
            builder.Entity<ProductVariantCharacteristic>()
                .HasIndex(pvc => pvc.ProductVariantId).HasDatabaseName("idx_ProductVariantCharacteristic_ProductVariantId");
            builder.Entity<ProductVariantCharacteristic>()
                .HasIndex(pvc => pvc.CharacteristicId).HasDatabaseName("idx_ProductVariantCharacteristic_CharacteristicId");
            builder.Entity<ProductVariantCharacteristic>()
                .HasIndex(pvc => pvc.CharacteristicValueId).HasDatabaseName("idx_ProductVariantCharacteristic_CharacteristicValueId");
            builder.Entity<Currency>().HasIndex(c => c.Code).HasDatabaseName("idx_Currency_Code");
            builder.Entity<Currency>().HasIndex(c => c.RateToBase).HasDatabaseName("idx_Currency_RateToBase");
            builder.Entity<ShippingFee>().HasIndex(c => c.CompanyId).HasDatabaseName("idx_ShippingFee_CompanyId");
            builder.Entity<WalletTransaction>().HasIndex(wt => wt.UserId).HasDatabaseName("idx_WalletTransaction_UserId");
            builder.Entity<WalletTransaction>().HasIndex(wt => wt.Type).HasDatabaseName("idx_WalletTransaction_Type");
            builder.Entity<Wallet>().HasIndex(w => w.Type).HasDatabaseName("idx_Wallet_Type");
            builder.Entity<WalletTransaction>().HasIndex(wt => wt.WalletType).HasDatabaseName("idx_WalletTransaction_WalletType");
            builder.Entity<WalletTransaction>().HasIndex(wt => wt.CurrencyBase).HasDatabaseName("idx_WalletTransaction_CurrencyBase");
            builder.Entity<WalletTransaction>().HasIndex(wt => wt.Balance).HasDatabaseName("idx_WalletTransaction_Balance");
            builder.Entity<SellerPayment>().HasIndex(c => c.CompanyId).HasDatabaseName("idx_SellerPayment_CompanyId");
            builder.Entity<SellerPayment>()
                .HasIndex(sp => new { sp.OrderId, sp.CompanyId })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE")
                .HasDatabaseName("ux_SellerPayment_Order_Company");
            builder.Entity<OrderProduct>().HasIndex(c => c.OrderId).HasDatabaseName("idx_OrderProduct_OrderId");
            builder.Entity<OrderProduct>().HasIndex(c => c.ProductVariantId).HasDatabaseName("idx_OrderProduct_ProductVariantId");
            builder.Entity<Notification>().HasIndex(c => c.UserId).HasDatabaseName("idx_Notification_UserId");
            builder.Entity<PromoCode>().HasIndex(c => c.Code).HasDatabaseName("idx_PromoCode_Code");
            builder.Entity<PromoCodeUsage>().HasIndex(c => c.PromoCodeId).HasDatabaseName("idx_PromoCodeUsage_PromoCodeId");
            builder.Entity<Employee>().HasIndex(c => c.UserId).HasDatabaseName("idx_Employee_UserId");

            // ============================================================
            // NOUVEAUX INDEX POUR OPTIMISATION PERFORMANCE (Étape 1)
            // ============================================================
            
            // Orders - Index composites pour requêtes fréquentes
            builder.Entity<Order>().HasIndex(o => new { o.UserId, o.Status }).HasDatabaseName("idx_Order_UserId_Status");
            builder.Entity<Order>().HasIndex(o => new { o.Status, o.CreatedAt }).HasDatabaseName("idx_Order_Status_CreatedAt");
            builder.Entity<Order>().HasIndex(o => o.Status).HasDatabaseName("idx_Order_Status");
            
            // OrderProducts - Clé pour requêtes par entreprise et statut
            builder.Entity<OrderProduct>().HasIndex(op => new { op.CompanyId, op.OrderId }).HasDatabaseName("idx_OrderProduct_CompanyId_OrderId");
            builder.Entity<OrderProduct>().HasIndex(op => op.CompanyId).HasDatabaseName("idx_OrderProduct_CompanyId");
            builder.Entity<OrderProduct>().HasIndex(op => op.Status).HasDatabaseName("idx_OrderProduct_Status");
            
            // AmbassadorCommissions - Index pour dashboards ambassadeur
            builder.Entity<AmbassadorCommission>().HasIndex(ac => new { ac.AmbassadorId, ac.Status }).HasDatabaseName("idx_AmbassadorCommission_AmbassadorId_Status");
            builder.Entity<AmbassadorCommission>().HasIndex(ac => ac.Status).HasDatabaseName("idx_AmbassadorCommission_Status");
            builder.Entity<AmbassadorCommission>().HasIndex(ac => new { ac.Status, ac.CreatedAt }).HasDatabaseName("idx_AmbassadorCommission_Status_CreatedAt");
            
            // Notifications - Index pour récupération rapide
            builder.Entity<Notification>().HasIndex(n => new { n.UserId, n.IsRead }).HasDatabaseName("idx_Notification_UserId_IsRead");
            builder.Entity<Notification>().HasIndex(n => new { n.UserId, n.CreatedAt }).HasDatabaseName("idx_Notification_UserId_CreatedAt");
            
            // UserActivityLogs - Index pour analytics et audit
            builder.Entity<UserActivityLog>().HasIndex(ual => ual.Timestamp).HasDatabaseName("idx_UserActivityLog_Timestamp");
            builder.Entity<UserActivityLog>().HasIndex(ual => new { ual.UserName, ual.Timestamp }).HasDatabaseName("idx_UserActivityLog_UserName_Timestamp");
            
            // Products - Index pour recherche et filtrage
            builder.Entity<Product>().HasIndex(p => new { p.IsDeleted, p.CategoryId }).HasDatabaseName("idx_Product_IsDeleted_CategoryId");
            builder.Entity<Product>().HasIndex(p => p.IsPromoted).HasDatabaseName("idx_Product_IsPromoted");
            
            // ProductVariants - Index pour disponibilité
            builder.Entity<ProductVariant>().HasIndex(pv => new { pv.IsAvailable, pv.IsDeleted, pv.ProductId }).HasDatabaseName("idx_ProductVariant_IsAvailable_IsDeleted_ProductId");
            builder.Entity<ProductVariant>().HasIndex(pv => pv.IsAvailable).HasDatabaseName("idx_ProductVariant_IsAvailable");
            
            // Referrals - Index pour statistiques ambassadeur
            builder.Entity<Referral>().HasIndex(r => new { r.ReferrerId, r.CreatedAt }).HasDatabaseName("idx_Referral_ReferrerId_CreatedAt");
            
            // AmbassadorSubscriptions - Index composites pour validation rapide
            builder.Entity<AmbassadorSubscription>().HasIndex(s => new { s.UserId, s.Status, s.EndDate }).HasDatabaseName("idx_AmbassadorSubscription_UserId_Status_EndDate");
            
            // Transactions - Index pour historique financier
            builder.Entity<Transaction>().HasIndex(t => new { t.UserId, t.Status }).HasDatabaseName("idx_Transaction_UserId_Status");
            builder.Entity<Transaction>().HasIndex(t => new { t.Status, t.CreatedAt }).HasDatabaseName("idx_Transaction_Status_CreatedAt");
            builder.Entity<Transaction>()
                .HasIndex(t => t.TransactionId)
                .IsUnique()
                .HasFilter("\"TransactionId\" IS NOT NULL")
                .HasDatabaseName("ux_Transaction_ExternalId");
            builder.Entity<Transaction>()
                .HasIndex(t => t.OrderId)
                .IsUnique()
                .HasFilter("\"OrderId\" IS NOT NULL")
                .HasDatabaseName("ux_Transaction_OrderId");

            builder.Entity<PlatformTransaction>()
                .HasIndex(pt => new { pt.ExternalTransactionId, pt.Category })
                .IsUnique()
                .HasFilter("\"ExternalTransactionId\" IS NOT NULL AND \"IsDeleted\" = FALSE")
                .HasDatabaseName("ux_PlatformTransaction_External_Category");
            
            // Companies - Index pour vérification existence rapide
            builder.Entity<Company>().HasIndex(c => new { c.Id, c.IsDeleted }).HasDatabaseName("idx_Company_Id_IsDeleted");
            builder.Entity<Company>().HasIndex(c => c.IsVerified).HasDatabaseName("idx_Company_IsVerified");






        }

        public override int SaveChanges()
        {
            NormalizeDateTimeOffsetsToUtc();

            // Appel des méthodes existantes
            SyncWalletBalances();
            SyncStockProductVariant();
            UpdateSubscriptionEndDates();
            // SyncPointBalances(); // TODO: Implémenter cette méthode

            // Récupérer toutes les entités Order et OrderProduct modifiées
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is OrderProduct || e.Entity is Order)
                .ToList();

            foreach (var entry in entries)
            {
                if (entry.Entity is OrderProduct orderProduct)
                {
                    // Recalculer le total de la commande
                    var order = Orders.Include(o => o.Products)
                                    .ThenInclude(op => op.ProductVariant)
                                    .FirstOrDefault(o => o.Id == orderProduct.OrderId);

                    if (order != null)
                    {
                        order.TotalAmount = order.Products.Sum(p => p.PriceAtOrder * p.Quantity);
                    }
                }
            }

            foreach (var entry in ChangeTracker.Entries<Shipment>())
            {
                if (entry.State == EntityState.Modified || entry.State == EntityState.Added)
                {
                    var shipment = entry.Entity;
                    var orderProduct = OrderProducts
                        .FirstOrDefault(op => op.Id == shipment.OrderProductId);

                    if (orderProduct != null)
                    {
                        // Synchroniser le statut du produit avec celui du shipment
                        switch (shipment.Status)
                        {
                            case ShipmentStatus.InTransit:
                                orderProduct.Status = OrderProductStatus.Shipped;
                                break;
                            case ShipmentStatus.Delivered:
                                orderProduct.Status = OrderProductStatus.Delivered;
                                break;
                            case ShipmentStatus.Canceled:
                                orderProduct.Status = OrderProductStatus.Returned;
                                break;
                        }              
                    }  
                }
            }

            // ?? CORRECTION : Utilisez base.SaveChanges() UNE SEULE FOIS
            ApplyAudit();
            return base.SaveChanges();
        }
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            NormalizeDateTimeOffsetsToUtc();

            // Appel des méthodes existantes
            SyncWalletBalances();
            SyncStockProductVariant();
            UpdateSubscriptionEndDates();
            // SyncPointBalances(); // TODO: Implémenter cette méthode

            // Récupérer toutes les entités Order et OrderProduct modifiées
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is OrderProduct || e.Entity is Order)
                .ToList();

            foreach (var entry in entries)
            {
                if (entry.Entity is OrderProduct orderProduct)
                {
                    // Recalculer le total de la commande
                    var order = await Orders.Include(o => o.Products)
                                            .ThenInclude(op => op.ProductVariant)
                                            .FirstOrDefaultAsync(o => o.Id == orderProduct.OrderId, cancellationToken);

                    if (order != null)
                    {
                        order.TotalAmount = order.Products.Sum(p => p.PriceAtOrder * p.Quantity);
                    }
                }
            }
            foreach (var entry in ChangeTracker.Entries<Shipment>())
            {
                if (entry.State == EntityState.Modified || entry.State == EntityState.Added)
                {
                    var shipment = entry.Entity;
                    var orderProduct = await OrderProducts
                        .FirstOrDefaultAsync(op => op.Id == shipment.OrderProductId, cancellationToken);

                    if (orderProduct != null)
                    {
                        // Synchroniser le statut du produit avec celui du shipment
                        switch (shipment.Status)
                        {
                            case ShipmentStatus.InTransit:
                                orderProduct.Status = OrderProductStatus.Shipped;
                                break;
                            case ShipmentStatus.Delivered:
                                orderProduct.Status = OrderProductStatus.Delivered;
                                break;
                            case ShipmentStatus.Canceled:
                                orderProduct.Status = OrderProductStatus.Returned;
                                break;
                        }
                    }
                }
            }


            // Sauvegarder les changements dans la base de données
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void SyncWalletBalances()
        {
            foreach (var entry in ChangeTracker.Entries<Wallet>())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    var wallet = entry.Entity;
                    var user = Users.Find(wallet.UserId);
                    if (user != null && wallet.Type == WalletType.Customer)
                    {
                        user.Balance = wallet.Balance;
                    }
                }
            }
        }

        private void SyncStockProductVariant()
        {
            foreach (var entry in ChangeTracker.Entries<Stock>())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    var stock = entry.Entity;
                    var pv = ProductVariants.Find(stock.ProductVariantId);
                    if (pv != null)
                    {
                        pv.Quantity = stock.Quantity;
                        if(stock.Quantity == 0){
                            pv.IsAvailable = false;
                        }
                    }
                }
            }
            
        }

        private void NormalizeDateTimeOffsetsToUtc()
        {
            // Npgsql (timestamptz) n'accepte que des DateTimeOffset en UTC (offset 0)
            var entries = ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified);

            foreach (var entry in entries)
            {
                foreach (var prop in entry.Properties)
                {
                    var clrType = prop.Metadata.ClrType;

                    if (clrType == typeof(DateTimeOffset))
                    {
                        if (prop.CurrentValue is DateTimeOffset dto)
                            prop.CurrentValue = dto.ToUniversalTime();
                    }
                    else if (clrType == typeof(DateTimeOffset?))
                    {
                        if (prop.CurrentValue is DateTimeOffset dtoNullable)
                            prop.CurrentValue = dtoNullable.ToUniversalTime();
                    }
                }
            }
        }

        private void UpdateSubscriptionEndDates()
        { 
            foreach (var entry in ChangeTracker.Entries<AmbassadorSubscription>())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    var subscription = entry.Entity;
                    var plan = AmbassadorPlans.Find(subscription.PlanId);
                    if (plan != null)
                    {
                        subscription.CalculateEndDate(plan.DurationInDays);
                    }
                }
            }
        }


        private void ApplyAudit()
        {
            var userId = _httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var now = DateTimeOffset.UtcNow;

            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                    entry.Entity.ModifiedAt = now;
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.ModifiedBy = userId;
                    entry.Entity.Version = 1;
                    if (!entry.CurrentValues.Properties.Any(p => p.Name == nameof(BaseEntity.IsDeleted)))
                    {
                        // no-op
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.ModifiedAt = now;
                    entry.Entity.ModifiedBy = userId;
                    entry.Entity.Version = (entry.Entity.Version <= 0 ? 1 : entry.Entity.Version + 1);

                    // empêcher la modification des données d'audit initiales
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                }
            }
        }
    }
}
