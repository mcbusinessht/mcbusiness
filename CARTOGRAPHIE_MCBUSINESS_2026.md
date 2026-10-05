## 🗄️ MODÈLES DE DONNÉES (85 entités)

### 👤 Utilisateurs et Identité
- **ApplicationUser** - Utilisateur principal (hérite d'IdentityUser)
- **UserActivityLog** - Journal d'activité utilisateur
- **UserView** - Historique de vues
- **UserTreeNode** - Arbre hiérarchique utilisateurs (MLM)

### 🎯 Système Ambassadeur
- **AmbassadorPlan** - Plans d'ambassadeur
- **AmbassadorPlanBonus** - Bonus associés aux plans
- **AmbassadorPlanBonusMapping** - Mapping plans-bonus
- **AmbassadorSubscription** - Abonnements ambassadeurs
- **AmbassadorCommission** - Commissions calculées
- **Referral** - Système de parrainage
- **Commission** - Commissions génériques

### 🛍️ E-Commerce Core
- **Product** - Produits
- **ProductImage** - Images produits
- **ProductVariant** - Variantes de produits
- **ProductVariantCharacteristic** - Caractéristiques des variantes
- **ProductReview** - Avis clients
- **ProductPriceHistory** - Historique des prix
- **Category** - Catégories
- **Brand** - Marques
- **Characteristic** - Caractéristiques
- **CharacteristicValue** - Valeurs de caractéristiques
- **PriceBreak** - Prix par palier (gros)

### 🛒 Panier et Commandes
- **Cart** - Panier
- **CartItem** - Articles du panier
- **Order** - Commandes
- **OrderProduct** - Produits commandés
- **ReturnRequest** - Demandes de retour

### 💳 Paiements et Transactions
- **Payment** - Paiements
- **PaymentNotification** - Notifications de paiement
- **BankTransferPayment** - Virements bancaires
- **StripeTransaction** - Transactions Stripe
- **StripeConfig** - Configuration Stripe
- **PlatformTransaction** - Transactions plateforme
- **Transaction** - Transactions génériques
- **Wallet** - Portefeuilles utilisateurs
- **WalletTransaction** - Transactions portefeuille

### 🚚 Livraison et Logistique
- **Shipment** - Expéditions
- **ShipmentMethod** - Méthodes de livraison
- **ShippingFee** - Frais de port
- **DeliveryPerson** - Livreurs
- **Address** - Adresses

### 🏢 Entreprises et Fournisseurs
- **Company** - Entreprises/Fournisseurs
- **CompanySector** - Secteurs d'activité
- **SupplierPayment** - Paiements fournisseurs
- **Employee** - Employés
- **EmployeePerformance** - Performance employés
- **EmployeeTask** - Tâches employés
- **Expense** - Dépenses

### 🎁 Promotions et Marketing
- **PromoCode** - Codes promo
- **PromoCodeUsage** - Utilisation codes promo
- **PromoCampaign** - Campagnes promotionnelles
- **Newsletter** - Newsletters

### 🏆 Système de Points
- **UserPoint** - Points utilisateurs
- **PointTransaction** - Transactions de points
- **PointEvent** - Événements points
- **PointConversion** - Conversion de points

### 💼 Services IT
- **ITService** - Services IT
- **ITServiceFeature** - Fonctionnalités services
- **ServiceOrder** - Commandes de services
- **ServiceTag** - Tags services

### 📦 Gestion Stock
- **Stock** - Stocks
- **StockReservation** - Réservations de stock

### 💬 Messagerie et Notifications
- **Conversation** - Conversations
- **Message** - Messages
- **Notification** - Notifications
- **Notice** - Avis/Annonces

### 🌍 Géolocalisation
- **Country** - Pays
- **State** - États/Régions
- **City** - Villes
- **Neighborhood** - Quartiers

### ⚙️ Configuration Système
- **SystemConfiguration** - Configuration système
- **SystemMedia** - Médias système
- **Currency** - Devises
- **EmailSettings** - Paramètres email
- **ReCaptchaSettings** - Paramètres ReCaptcha
- **CookieSettings** - Paramètres cookies
- **AdminParams** - Paramètres admin

### 🔍 Autres
- **SearchHistory** - Historique recherches
- **Post** - Articles/Blog
- **BaseEntity** - Classe de base (héritée par entités)
- **CalculBonus** - Calculs de bonus

---

## 🔧 SERVICES (41 services)

### 👨‍💼 Services Ambassadeur
- **AmbassadorCommissionService** - Gestion commissions
- **AmbassadorCommissionCalculator** - Calcul commissions
- **AmbassadorCommissionProcessor** - Traitement commissions
- **AmbassadorAccessService** - Contrôle d'accès
- **AmazonLikeDistributionService** - Distribution Amazon-like
- **ReferralService** - Gestion parrainages
- **ReferralCodeGenerator** - Génération codes parrainage

### 💰 Services Financiers
- **MonCashService** - Intégration MonCash
- **MonCashWebhookService** - Webhooks MonCash
- **StripeService** - Intégration Stripe
- **SecureFinancialService** - Sécurité financière
- **PlatformTransactionAuditService** - Audit transactions
- **SupplierPaymentService** - Paiements fournisseurs
- **PointTransactionService** - Transactions points

### 🛒 Services Commerce
- **OptimizedProductService** - Gestion produits optimisée
- **OptimizedCartService** - Gestion panier optimisée
- **OrderAmountService** - Calcul montants commandes
- **OrderStatusService** - Gestion statuts commandes
- **OrderAmbassadorIntegrationService** - Intégration commandes-ambassadeurs
- **CancelOrderService** - Annulation commandes
- **StockReservationService** - Réservation stocks

### 🚚 Services Logistique
- **ShipmentStatusHandler** - Gestion statuts expéditions
- **AddressService** - Gestion adresses

### 📧 Services Communication
- **EmailSender** (IEmailSender) - Envoi emails
- **NotificationService** - Gestion notifications
- **MessageService** - Gestion messages
- **NoticeService** - Gestion avis/annonces

### 🏢 Services Entreprise
- **CompanyService** - Gestion entreprises
- **DashboardService** - Tableaux de bord

### 🔍 Services Utilitaires
- **CacheService** - Gestion cache
- **PaginationService** - Pagination
- **ReCaptchaService** - Validation ReCaptcha
- **ImageOptimizationService** - Optimisation images
- **AuditService** - Audit système

### 🔄 Services Background
- **UserActivityBackgroundService** - Activité utilisateur en arrière-plan
- **CleanupService** - Nettoyage
- **CookieCleanupService** - Nettoyage cookies
- **SubscriptionStatusUpdater** - Mise à jour statuts abonnements
- **TransactionStatusUpdater** - Mise à jour statuts transactions

### 📊 Services Métriques
- **AdvancedMetricsService** - Métriques avancées
- **CookieConsentService** - Gestion consentement cookies

---

## 🎮 CONTRÔLEURS ET API

### API REST (3 contrôleurs)
- **ChatController** - API chat temps réel
- **CookieConsentController** - API consentement cookies
- **SearchController** (API) - API recherche

### Contrôleurs MVC (5 contrôleurs)
- **AmbassadorController** - Gestion ambassadeurs
- **PaymentStatutController** - Statuts paiements
- **PayoutController** - Paiements sortants
- **SearchController** - Recherche
- **TimeZoneController** - Gestion fuseaux horaires

---

## 🔌 MIDDLEWARES (3 middlewares)

- **AmbassadorTrackingMiddleware** - Suivi des clics ambassadeurs (cookies)
- **UserActivityMiddleware** - Enregistrement activité utilisateur
- **SessionErrorHandlingMiddleware** - Gestion erreurs de session

---

## 🎯 FILTRES ET ATTRIBUTS

### Filtres
- **AdminAccessFilter** - Contrôle d'accès admin

### Attributs de Validation
- **AllowedExtensionsAttribute** - Validation extensions fichiers
- **MaxFileSizeAttribute** - Validation taille fichiers
- **IdempotentActionAttribute** - Actions idempotentes

---

## 🔄 HUBS SIGNALR (Temps Réel)

- **ChatHub** - Chat en temps réel
- **NotificationHub** - Notifications en temps réel

---

## 🎨 PAGES RAZOR (Interface Utilisateur)

### Pages Publiques
- Index.cshtml - Page d'accueil
- About.cshtml - À propos
- Contact.cshtml - Contact
- Privacy.cshtml - Politique de confidentialité
- TermsAndConditions.cshtml - CGU
- SellerTermsAndConditions.cshtml - CGU vendeurs
- HelpUs.cshtml - Aide
- Search.cshtml - Recherche

### E-Commerce
- MyCart.cshtml - Mon panier
- PlaceOrder.cshtml - Passer commande

### Compte Utilisateur
- Login.cshtml - Connexion
- SignUp.cshtml - Inscription
- ForgotPassword.cshtml - Mot de passe oublié
- ResetPassword.cshtml - Réinitialisation mot de passe
- ConfirmEmail.cshtml - Confirmation email
- VerifyEmail.cshtml - Vérification email
- TwoFactor.cshtml - Authentification 2FA
- EnableTwoFactorAuthentication.cshtml - Activer 2FA

### Commandes
- MyOrders.cshtml - Mes commandes
- OrderDetails.cshtml - Détails commande
- OrderTracking.cshtml - Suivi commande

### Paiements
- Payment.cshtml - Paiement
- PaymentMethods.cshtml - Méthodes de paiement
- MonCash.cshtml - Paiement MonCash
- ThankYou.cshtml - Merci
- Succes.cshtml - Succès

### Boutique
- Shop/Index.cshtml - Boutique
- Shop/Categories/Index.cshtml - Catégories

### Plans Ambassadeur
- Plans/Index.cshtml - Plans d'abonnement

### Services
- Services/Index.cshtml - Services IT

### Entreprises
- Companies/Index.cshtml - Liste entreprises
- Companies/Details.cshtml - Détails entreprise

### Espace Utilisateur
- User/Dashboard.cshtml - Tableau de bord
- User/Profil.cshtml - Profil
- User/Addresses.cshtml - Adresses
- User/Notifications.cshtml - Notifications
- User/Transaction.cshtml - Transactions
- User/Changeplan.cshtml - Changer de plan

### Espace Ambassadeur
- AmbassadorSpace/Dashboard.cshtml - Tableau de bord
- AmbassadorSpace/AmbassadorCommissions.cshtml - Commissions
- AmbassadorSpace/AmbassadorLinks.cshtml - Liens affiliés
- AmbassadorSpace/AmbassadorNetwork.cshtml - Réseau

### Espace Fournisseur
- Supplier/Dashboard.cshtml - Tableau de bord
- Supplier/NoCompany.cshtml - Pas d'entreprise
- Supplier/Notifications.cshtml - Notifications
- Supplier/Products/* - Gestion produits
- Supplier/Orders/* - Gestion commandes
- Supplier/Payments/* - Paiements
- Supplier/Shipments/* - Expéditions
- Supplier/DeliveryPersons/* - Livreurs
- Supplier/Profil/* - Profil

### Espace Employé
- EmployeeSpace/Dashboard.cshtml - Tableau de bord
- EmployeeSpace/NoEmployee.cshtml - Pas d'employé

### Espace Admin (Backoffice complet)
- Admin/Dashboard.cshtml - Tableau de bord admin
- Admin/Products/* - Gestion produits
- Admin/Categories/* - Gestion catégories
- Admin/Orders/* - Gestion commandes
- Admin/Users/* - Gestion utilisateurs
- Admin/Admins/* - Gestion admins
- Admin/Transactions/* - Transactions
- Admin/Commissions/* - Commissions
- Admin/Plans/* - Plans ambassadeur
- Admin/Subscriptions/* - Abonnements
- Admin/Promotions/* - Promotions
- Admin/Stocks/* - Stocks
- Admin/Shipments/* - Expéditions
- Admin/Services/* - Services IT
- Admin/Notifications/* - Notifications
- Admin/Notices/* - Avis/Annonces
- Admin/Medias/* - Médias
- Admin/Currencies/* - Devises
- Admin/Addresses/* - Adresses
- Admin/BankTransferPayments/* - Virements
- Admin/SupplierPayments/* - Paiements fournisseurs
- Admin/Payouts/* - Paiements sortants
- Admin/Referrals/* - Parrainages
- Admin/GestionRoles/* - Gestion des rôles
- Admin/ManageEmployees/* - Gestion employés
- Admin/UserActivityLogs/* - Logs d'activité
- Admin/FinancialReconciliation/* - Réconciliation financière

---

## 🔐 SÉCURITÉ ET AUTHENTIFICATION

### Authentification
- ASP.NET Core Identity
- Authentification à deux facteurs (2FA)
- Confirmation email obligatoire
- Réinitialisation mot de passe sécurisée

### Autorisation
- Rôles: Admin, Supplier, Ambassador, Employee, User
- Politiques personnalisées
- Filtres d'accès (AdminAccessFilter)
- Middleware de tracking

### Protection
- ReCaptcha sur formulaires critiques
- Rate Limiting (AspNetCoreRateLimit)
- Protection CSRF
- Protection contre double-soumission (IdempotentAction)
- Validation fichiers (taille, extensions)

### Cookies et Sessions
- Gestion consentement cookies (RGPD)
- Cookie de tracking ambassadeur (30 jours)
- Session sécurisée
- Middleware de gestion erreurs session

---

## 💾 BASE DE DONNÉES

### Configuration
- **Provider:** PostgreSQL
- **ORM:** Entity Framework Core 9.0
- **Migrations:** Activées (dossier Migrations/)
- **Connection String:** Configurée dans appsettings.json

### Stratégie
- Code First avec migrations
- Relations configurées dans AppDbContext
- Indexes pour optimisation
- Soft delete sur certaines entités

---

## 💳 INTÉGRATIONS PAIEMENT

### Stripe
- Paiements par carte
- Webhooks configurés
- Gestion des transactions
- Configuration via StripeConfig

### MonCash (Haïti)
- Paiement mobile
- Webhooks MonCash
- Service dédié (MonCashService)

### Virement Bancaire
- Gestion manuelle
- Upload de preuve
- Validation admin

---

## 📧 SYSTÈME EMAIL

### Templates HTML
- auth_2FA.html - Code 2FA
- confirm_email.html - Confirmation email
- login_notification.html - Notification connexion
- reset_passw.html - Réinitialisation
- welcome_user.html - Bienvenue
- new_message.html - Nouveau message
- new_notification.html - Nouvelle notification
- subscription_expired.html - Abonnement expiré
- subscription_notice.html - Avis abonnement

### Service
- EmailSender implémente IEmailSender
- Configuration SMTP dans appsettings.json
- Templates personnalisables

---

## 🎨 FRONTEND

### Technologies
- Razor Pages
- Bootstrap
- JavaScript vanilla
- SignalR Client

### Scripts JavaScript
- site.js - Scripts généraux
- dark-mode.js - Mode sombre
- dashboard-charts.js - Graphiques
- cookie-helpers.js - Gestion cookies
- anti-double-submit.js - Protection double soumission
- performance-optimizations.js - Optimisations
- signup-validation.js - Validation inscription
- supplier-sidebar.js - Sidebar fournisseur

### CSS
- site.css - Styles généraux
- style.css - Styles personnalisés
- dark-mode.css - Mode sombre
- dark-mode-enhanced.css - Mode sombre amélioré
- ambassador-access.css - Styles ambassadeur
- dashboard-enhancements.css - Améliorations dashboard

### Assets
- Images (logos, icônes, illustrations)
- Uploads (produits, preuves, logos entreprises)
- Emails templates HTML

---

## 🔄 SYSTÈME AMBASSADEUR DÉTAILLÉ

### Fonctionnement
1. **Inscription ambassadeur** - Choix d'un plan
2. **Génération code parrainage** - Code unique
3. **Tracking** - Middleware capture clics via cookies (30j)
4. **Commande** - Attribution à l'ambassadeur
5. **Calcul commission** - Selon plan et montant
6. **Distribution** - Type Amazon (multi-niveaux)
7. **Paiement** - Via Payouts

### Plans Ambassadeur
- Plans configurables avec bonus
- Mapping flexible plans-bonus
- Abonnements renouvelables
- Mise à jour automatique statuts

### Commissions
- Calcul automatique à chaque commande
- Multi-niveaux (Amazon-like)
- Historique complet
- Audit des transactions

### Tracking
- Cookie-based (30 jours)
- Pas de query string en URL
- Liens personnalisés ambassadeur
- Statistiques détaillées

---

## 📊 SYSTÈME DE POINTS

### Fonctionnement
- **PointEvent** - Événements donnant des points
- **PointTransaction** - Transactions de points
- **UserPoint** - Solde de points utilisateur
- **PointConversion** - Conversion points vers argent/réductions

### Usages
- Fidélisation clients
- Récompenses achats
- Parrainage
- Événements spéciaux

---

## 🗂️ SCRIPTS UTILITAIRES

### Scripts SQL
- **BackupSetup.sql** - Configuration sauvegardes
- **CreerAmbassadeurTest.sql** - Créer ambassadeur test
- **DiagnosticAmbassadorCommissions.sql** - Diagnostic commissions
- **MigrateHistoricalPromoCodeAudit.sql** - Migration audit codes promo
- **OptimizeDatabase.sql** - Optimisation BDD
- **TestAmbassadorCommissionWorkflow.sql** - Test workflow ambassadeur
- **VerifierCodeReferral.sql** - Vérifier codes parrainage

### Scripts PowerShell
- **Fix-EncodingIssues.ps1** - Correction encodage

### Documentation
- **DockerBackupCommands.md** - Commandes backup Docker

---

## 🐳 DOCKER

### Configuration
- **Dockerfile** - Image application
- **docker-compose.yml** - Orchestration services
- **.dockerignore** - Fichiers exclus

### Services
- Application ASP.NET Core
- PostgreSQL
- (Possiblement Redis pour cache)

---

## ⚙️ CONFIGURATION

### appsettings.json
- ConnectionStrings (PostgreSQL)
- EmailSettings (SMTP)
- StripeConfig
- ReCaptchaSettings
- CookieSettings
- Rate Limiting
- Logging

### Environnements
- appsettings.Development.json
- appsettings.json (Production)
- .env pour secrets Docker

### Secrets
- Clés de chiffrement (keys/)
- Configuration rclone (config/secrets/)

---

## 📈 FONCTIONNALITÉS AVANCÉES

### Performance
- Cache distribué
- Optimisation images (ImageSharp)
- Services optimisés (OptimizedCartService, OptimizedProductService)
- Pagination efficace
- Rate limiting

### Audit et Monitoring
- UserActivityLog - Toutes les actions utilisateurs
- AuditService - Audit système
- PlatformTransactionAuditService - Audit financier
- Logs structurés

### Temps Réel
- Chat (ChatHub)
- Notifications (NotificationHub)
- Mises à jour dashboard

### SEO et Marketing
- sitemap.xml
- Google Search Console (googlecb80cd8ba7e5b73f.html)
- Newsletter
- Historique recherches
- Campagnes promo

---

## 🧪 HELPERS ET UTILITAIRES

### Helpers
- **IdempotencyHelper** - Prévention double traitement

### Interfaces
- **IEmailSender** - Interface envoi email

---

## 📚 DOCUMENTATION EXISTANTE

Le projet contient déjà plusieurs documentations:
- AMAZON_LIKE_IMPLEMENTATION_GUIDE.md
- AUDIT_FINANCIER_ET_BUGS_MCBUSINESS.md
- CARTOGRAPHIE_COMPLETE_MCBUSINESS.md (ancienne version)
- CORRECTION_PAGE_PAIEMENT_PLANS.md
- CORRECTION_PAGE_PAYMENT_QUERY_STRING.md
- CORRECTIONS_COMMISSIONS_AMBASSADEUR_FINAL.md
- Database_Cartography_FriKob.html
- DIAGNOSTIC_COMMISSION_AMBASSADEUR_COMPLET.md
- GUIDE_UTILISATION_DIAGNOSTIC_AMBASSADEUR.md
- RAPPORT_AUDIT_CODES_PROMO_FINAL.md
- RAPPORT_TRACKING_AMBASSADEUR_COOKIES_SESSION.md
- SYSTEM_FINANCIAL_ANALYSIS.md
- SYSTEME_AMBASSADOR_COOKIES_ONLY_FINAL.md
- SYSTEMES_PARRAINAGE_SEPARATION.md
- TEST_MANUEL_AMBASSADEUR.md

---

## 🔑 POINTS CLÉS DU PROJET

### ✅ Points Forts
- Architecture propre et modulaire
- Nombreux services métier bien séparés
- Système ambassadeur complet et sophistiqué
- Multi-tenant avec gestion entreprises
- Sécurité renforcée (2FA, ReCaptcha, Rate Limiting)
- Audit complet des transactions
- Optimisations performance
- Documentation abondante

### ⚠️ Points d'Attention
- Complexité du système ambassadeur
- Multiples systèmes de paiement à maintenir
- Gestion des stocks et réservations
- Synchronisation temps réel (SignalR)
- Conformité RGPD (cookies)

### 🎯 Domaines Fonctionnels
1. **E-commerce** - Vente produits multi-vendeurs
2. **Services IT** - Vente services en ligne
3. **MLM/Affiliation** - Système ambassadeur multi-niveaux
4. **Gestion entreprise** - CRM/ERP light
5. **Messagerie** - Chat temps réel
6. **Logistique** - Gestion livraisons
7. **Finance** - Paiements, commissions, portefeuilles

---

## 📞 STACK TECHNIQUE COMPLÈTE

### Backend
- **Framework:** ASP.NET Core 9.0
- **Langage:** C# 12
- **Pattern:** MVC + Razor Pages
- **ORM:** Entity Framework Core 9.0
- **BDD:** PostgreSQL (Npgsql)
- **Cache:** In-Memory + AspNetCoreRateLimit
- **Real-time:** SignalR

### Bibliothèques Principales
- **Identity:** Microsoft.AspNetCore.Identity.EntityFrameworkCore
- **Paiements:** Stripe SDK, MonCash (custom)
- **Images:** SixLabors.ImageSharp
- **Email:** Custom (EmailSender)
- **Device Detection:** DeviceDetector.NET
- **Rate Limiting:** AspNetCoreRateLimit
- **Humanization:** Humanizer
- **BDD:** LiteDB (secondaire?), Npgsql.EntityFrameworkCore.PostgreSQL

### DevOps
- **Containerisation:** Docker
- **Orchestration:** Docker Compose
- **CI/CD:** Non visible dans le projet
- **Monitoring:** Logs structurés

### Sécurité
- **Auth:** ASP.NET Core Identity
- **2FA:** Built-in Identity
- **Captcha:** ReCaptcha v3
- **Rate Limiting:** AspNetCoreRateLimit
- **Data Protection:** ASP.NET Core Data Protection (keys/)

---

## 🎓 CONCLUSION

MCBusiness est une **plateforme e-commerce complète et sophistiquée** avec un système MLM/affiliation avancé. Le projet est bien structuré avec une séparation claire des responsabilités (services, contrôleurs, pages). 

La complexité réside principalement dans:
- Le système de commissions multi-niveaux (Amazon-like)
- La gestion multi-tenant (fournisseurs/entreprises)
- Les multiples flux de paiement
- L'audit et la sécurité financière

Le code montre une attention particulière à la sécurité, la performance et l'audit, avec de nombreux services dédiés et une documentation extensive.

---

**Date de création:** 16 Mars 2026  
**Version du document:** 1.0  
**Auteur:** Rovo Dev (Cartographie automatique)
