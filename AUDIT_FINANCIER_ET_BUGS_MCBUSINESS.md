# Rapport d'analyse : Cartographie financière et bugs – MCBusiness

**Date :** 2026  
**Périmètre :** Transactions financières, audit, répartition Amazon-like, réconciliation.

---

## 1. Cartographie détaillée du système financier

### 1.1 Entités et rôles

| Entité | Rôle | Tables / champs clés |
|--------|------|----------------------|
| **Transaction** | Mouvement utilisateur (dépôt, retrait, transfert, paiement) | `UserId`, `Amount`, `Type`, `Status`, `OrderId` (réf.), `TransactionId` |
| **Wallet** | Solde utilisateur | `UserId`, `Balance`, `LastUpdated`, `UpdatedAt` |
| **WalletTransaction** | Ligne d’historique wallet (audit solde) | `WalletId`, `Amount`, `Balance` (solde après opération), `Type` |
| **Payment** | Paiement client (commande, abo, service) | `OrderId` / `AmbassadorSubscriptionId` / `ServiceOrderId`, `Amount`, `Status`, `PromoCodeUsageId` |
| **PlatformTransaction** | Comptabilité plateforme (audit) | `Type` (Revenue/Expense), `Category`, `Amount`, `OrderId`, `WalletId` (wallet plateforme) |
| **SupplierPayment** | Paiement vers un vendeur (par commande/company) | `OrderId`, `CompanyId`, `ProductTotal`, `SupplierAmount`, `PlatformCommission*`, `ActualPaidAmount`, `PromoDiscountAmount`, `AmbassadorCommissionAmount` |
| **Wallet plateforme** | Solde global MCBusiness | Utilisateur `ReferralCode == "00000000"`, un `Wallet` associé |

### 1.2 Flux financiers mappés

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ 1. PAIEMENT CLIENT (commande)                                                │
│    Payment (Completed) → RecordOrderRevenueAsync(OrderId, Amount)             │
│    → PlatformTransaction (Revenue, OrderPayment) + Wallet plateforme +=       │
│    Appelé depuis : Payment.cshtml.cs, MonCashWebhookService, MonCashService, │
│    StripeService                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ 2. PAIEMENT PLAN AMBASSADEUR                                                 │
│    Payment (AmbassadorSubscriptionId) → RecordOrderRevenueAsync(...)         │
│    → BUG : OrderId passé = int.Parse(TransactionId) → crash / jamais audité  │
│    Devrait : RecordSubscriptionPaymentAsync(userId, amount, subscriptionId) │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ 3. LIVRAISON COMPLÈTE → PAIEMENT VENDEUR (par company)                       │
│    SupplierPaymentService.CreateSupplierPaymentIfEligibleAsync(orderId, companyId) │
│    → AmazonLikeDistributionService.CalculateDistributionAsync()             │
│    → SupplierPayment créé (SupplierAmount = netSupplierAmount local)         │
│    → RecordDistributionAsync(distribution) → 1 PlatformTransaction          │
│      SupplierPayment + (optionnel) CommissionReferrer + (optionnel) Promo   │
│    → Wallet plateforme -= (SupplierAmount + Ambassador + Promo)              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ 4. DÉPÔT / RETRAIT UTILISATEUR (wallet)                                      │
│    SecureFinancialService.ProcessDepositAsync / ProcessWithdrawalAsync       │
│    → Transaction + WalletTransaction + mise à jour Wallet utilisateur        │
│    → RecordUserDepositAsync / RecordUserWithdrawalAsync (audit plateforme)  │
│    → Wallet plateforme += dépôt / -= (retrait + frais)                       │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ 5. TRANSFERT ENTRE UTILISATEURS                                             │
│    SecureFinancialService.ProcessTransferAsync                               │
│    → Transaction (x2) + WalletTransaction (x2) + Wallet (x2)                 │
│    → AUCUN enregistrement dans PlatformTransaction (non tracé côté plateforme) │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 1.3 Formules de répartition (Amazon-like)

- **AmazonLikeDistributionService** (calcul) :
  - `basePlatformCommission = ProductTotal * supplierCommissionRate` (commission sur produits uniquement)
  - `PlatformCommission = basePlatformCommission + AmbassadorCommission + PromoDiscountAmount`
  - `SupplierAmount = grossAmount - PlatformCommission` → le vendeur subit ambassadeur + promo dans son montant.
- **SupplierPaymentService** (montant réellement versé au vendeur) :
  - `basePlatformCommission = grossAmount * distribution.SupplierCommissionRate` (commission sur produit + livraison)
  - `netSupplierAmount = grossAmount - basePlatformCommission` → vendeur reçoit bien un pourcentage fixe du brut (modèle “Amazon-like”).
- **Audit (RecordDistributionAsync)** utilise `distribution.SupplierAmount` (calcul du service), pas `payment.SupplierAmount` (netSupplierAmount). Donc le débit du wallet plateforme ne correspond pas au montant réellement versé au vendeur.

---

## 2. Bugs logiques identifiés

### 2.1 Critique – Incohérence montant vendeur vs audit

**Fichiers :** `Services/SupplierPaymentService.cs`, `Services/PlatformTransactionAuditService.cs`, `Services/AmazonLikeDistributionService.cs`

**Problème :**  
Le montant enregistré en audit (débit du wallet plateforme) n’est pas le montant réellement versé au vendeur.

- **SupplierPayment** est créé avec `SupplierAmount = netSupplierAmount` = `grossAmount - basePlatformCommission` (commission sur **grossAmount**), donc vendeur = pourcentage fixe du brut (ex. 90 %).
- **DistributionResult.SupplierAmount** = `grossAmount - (ProductTotal * rate + Ambassador + Promo)` (commission sur **ProductTotal** uniquement, et vendeur supporte ambassadeur + promo).
- **RecordDistributionAsync** débite `distribution.SupplierAmount` du wallet plateforme.

Conséquence : la plateforme débite moins que ce qu’elle verse au vendeur → solde wallet plateforme trop élevé, incohérence comptable.

**Correction recommandée :**  
Dans `RecordDistributionAsync`, utiliser le montant effectivement payé au vendeur (celui stocké dans `SupplierPayment.SupplierAmount`), pas `distribution.SupplierAmount`. Soit passer ce montant en paramètre, soit le déduire du `SupplierPayment` déjà créé (par ex. via un DTO ou une clé orderId+companyId).

---

### 2.2 Critique – Double (ou N×) comptage du coût promo (commandes multi-vendeurs)

**Fichiers :** `Services/AmazonLikeDistributionService.cs`, `Services/PlatformTransactionAuditService.cs`, `Services/SupplierPaymentService.cs`

**Problème :**  
Pour une commande avec **plusieurs companies** (plusieurs vendeurs), `CreateSupplierPaymentIfEligibleAsync` est appelé **une fois par (orderId, companyId)**. À chaque appel, `distribution.PromoDiscountAmount` = remise **totale** de la commande (`order.Payment.PromoCodeUsage.DiscountApplied`).  
`RecordDistributionAsync` enregistre alors une dépense **PromoDiscount** pour **chaque** company avec le **même** montant total. Le coût promo est donc compté N fois (N = nombre de vendeurs).

Conséquence : le wallet plateforme est débité N fois du montant de la remise → perte fictive et audit faux.

**Correction recommandée :**  
- Soit enregistrer la dépense promo **une seule fois** au niveau commande (hors boucle par company), par exemple dans un service dédié après création des tous les `SupplierPayment` de la commande.
- Soit répartir la remise par company (ex. au prorata du montant des produits de la company) et faire `PromoDiscountAmount` par company dans `DistributionResult`.

---

### 2.3 Critique – Audit revenu plan ambassadeur (crash / jamais enregistré)

**Fichier :** `Pages/Payments/Payment.cshtml.cs` (paiement plan ambassadeur par wallet)

**Problème :**  
Pour le paiement d’un plan ambassadeur, l’audit appelle :

```csharp
await _auditService.RecordOrderRevenueAsync(
    int.Parse(payment.TransactionId),  // TransactionId = GUID 12 caractères hex → int.Parse() lance OverflowException
    finalAmount,
    $"Paiement plan ambassadeur {Plan.Name} - Wallet"
);
```

`payment.TransactionId` provient de `GenerateUniqueOrderIdAsync()` (type string, 12 caractères hex). `int.Parse(...)` échoue → l’exception est catchée et le revenu abonnement **n’est jamais** enregistré dans `PlatformTransaction`. En plus, pour un abonnement on ne devrait pas utiliser “OrderRevenue” mais une catégorie dédiée.

Conséquence : revenus des abonnements ambassadeur non audités, réconciliation faussée.

**Correction recommandée :**  
- Remplacer cet appel par `RecordSubscriptionPaymentAsync(userId, finalAmount, subscription.Id, description)` (déjà défini dans `IPlatformTransactionAuditService`).
- Ne plus utiliser `RecordOrderRevenueAsync` pour les abonnements.

---

### 2.4 Majeur – Formule “Amazon-like” incohérente dans le calcul

**Fichier :** `Services/AmazonLikeDistributionService.cs`

**Problème :**  
La doc et le modèle “Amazon-like” indiquent que le vendeur reçoit un **pourcentage fixe du brut** (ex. 90 %), sans subir ambassadeur ni promo. Dans le code :

- Commission de base : `basePlatformCommission = result.ProductTotal * supplierCommissionRate` (pas de livraison).
- `result.SupplierAmount = grossAmount - (basePlatformCommission + AmbassadorCommission + PromoDiscountAmount)` → le vendeur supporte ambassadeur et promo.

Donc la formule dans `AmazonLikeDistributionService` n’est pas “Amazon-like” (vendeur fixe), alors que `SupplierPaymentService` applique bien un net vendeur fixe sur le brut. Incohérence entre “règle métier” et implémentation dans un des deux services.

**Correction recommandée :**  
Aligner la formule dans `AmazonLikeDistributionService` sur le modèle officiel :

- `basePlatformCommission = grossAmount * supplierCommissionRate`
- `SupplierAmount = grossAmount - basePlatformCommission` (indépendant de ambassador et promo)
- Garder `PlatformProfit`, `PlatformCosts`, etc. pour l’affichage / audit à partir de `ActualPaidAmount`, `SupplierAmount`, `AmbassadorCommission`, `PromoDiscountAmount`.

---

### 2.5 Majeur – Réconciliation : comparaison de montants incorrecte

**Fichier :** `Pages/Users/Admin/FinancialReconciliation/Index.cshtml.cs`

**Problème :**  
La détection des “montants incohérents” fait :

```csharp
var inconsistentAmounts = await _context.PlatformTransactions
    .Where(pt => pt.OrderId.HasValue &&
                _context.Payments.Any(p => p.OrderId == pt.OrderId &&
                                          p.Status == PaymentStatus.Completed &&
                                          p.Amount != pt.Amount))
    .CountAsync();
```

Pour une même commande, il existe **plusieurs** `PlatformTransaction` (revenu commande, paiement vendeur, commission ambassadeur, promo, etc.) avec des montants différents. Comparer **chaque** `pt.Amount` au `Payment.Amount` (montant total payé par le client) produit des faux positifs : toute ligne d’audit dont le montant n’est pas égal au total du paiement est signalée comme incohérente.

Conséquence : indicateur “incohérences de montant” inutilisable et trompeur.

**Correction recommandée :**  
- Pour la réconciliation “montant”, ne comparer que la **transaction de type Revenue / OrderPayment** pour cette commande : `pt.Category == PlatformTransactionCategory.OrderPayment` et `pt.Amount == payment.Amount` (en tenant compte du signe si besoin).
- Exclure les autres catégories (SupplierPayment, CommissionReferrer, PromoDiscount, etc.) de cette vérification de montant unique.

---

### 2.6 Moyen – Pas d’idempotence sur la distribution (audit)

**Fichiers :** `Services/PlatformTransactionAuditService.cs`, `Services/SupplierPaymentService.cs`

**Problème :**  
`RecordDistributionAsync` n’utilise pas d’`ExternalTransactionId` ni de garde “déjà enregistré pour (orderId, companyId)”. Si un mécanisme externe rappelait `CreateSupplierPaymentIfEligibleAsync` après un échec partiel (ex. timeout après `SaveChangesAsync` mais avant ou après l’audit), on pourrait soit :

- créer un second `SupplierPayment` (évité par le check `exists` sur OrderId+CompanyId),  
- soit, si le flux était modifié, enregistrer deux fois la même distribution en audit et débit du wallet deux fois.

Aujourd’hui le risque est limité par le check sur `SupplierPayment`, mais toute évolution (retry, job de rattrapage) pourrait introduire des doubles enregistrements d’audit.

**Correction recommandée :**  
- Introduire un identifiant unique pour l’audit de distribution, par ex. `ExternalTransactionId = $"dist_{orderId}_{companyId}"` et une catégorie dédiée (ou réutiliser une existante avec sens “distribution”).
- Au début de `RecordDistributionAsync`, appeler une logique du type `IsTransactionAlreadyAuditedAsync(externalId, category)` et sortir sans rien enregistrer si déjà audité.

---

### 2.7 Moyen – Transferts utilisateur non audités

**Fichier :** `Services/SecureFinancialService.cs`

**Problème :**  
`ProcessTransferAsync` met à jour les wallets utilisateurs et crée des `Transaction` et `WalletTransaction`, mais **n’appelle pas** le service d’audit plateforme. Les transferts entre utilisateurs ne sont donc pas visibles dans `PlatformTransaction`.

Conséquence : traçabilité incomplète côté plateforme (pas de vue centrale sur les flux entre utilisateurs).

**Correction recommandée :**  
- Soit ajouter une méthode d’audit du type `RecordUserTransferAsync(fromUserId, toUserId, amount, externalTransactionId)` qui enregistre une (ou deux) ligne(s) en `PlatformTransaction` (par ex. catégorie “Transfer” ou équivalent, selon votre modèle de catégories).
- Soit documenter explicitement que les transferts sont volontairement hors audit plateforme (et adapter la réconciliation en conséquence).

---

### 2.8 Mineur – Champs Wallet : LastUpdated vs UpdatedAt

**Fichiers :** `Models/Wallet.cs`, `Services/SecureFinancialService.cs`, `Services/PlatformTransactionAuditService.cs`

**Problème :**  
Le modèle `Wallet` expose `LastUpdated` et `UpdatedAt`.  
- `SecureFinancialService` met à jour `LastUpdated`.  
- `PlatformTransactionAuditService` met à jour `UpdatedAt`.

Conséquence : deux champs de “dernière mise à jour” peuvent diverger, ce qui peut prêter à confusion pour le support ou des rapports.

**Correction recommandée :**  
Mettre à jour les deux champs partout où le solde change, ou n’en garder qu’un (par ex. `UpdatedAt`) et le mettre à jour dans tous les services.

---

### 2.9 Mineur – PlatformProfit sans déduction ambassadeur

**Fichier :** `Services/AmazonLikeDistributionService.cs`

**Problème :**  
Ligne 140 :

```csharp
result.PlatformProfit = basePlatformCommission - result.PromoDiscountAmount;
```

La commission ambassadeur n’est pas déduite. Le “profit plateforme” affiché / snapshot est donc surestimé lorsqu’il y a un ambassadeur.

**Correction recommandée :**  
`result.PlatformProfit = basePlatformCommission - result.AmbassadorCommission - result.PromoDiscountAmount;` (ou équivalent cohérent avec votre définition du profit).

---

## 3. Synthèse des problèmes d’audit

| Problème | Gravité | Impact |
|----------|---------|--------|
| Montant audit distribution ≠ montant payé au vendeur | Critique | Solde wallet plateforme faux, réconciliation impossible |
| Coût promo compté N fois (N = nombre de vendeurs) | Critique | Débit plateforme exagéré, perte fictive |
| Revenu abonnement ambassadeur jamais audité (int.Parse) | Critique | Revenus manquants dans PlatformTransaction |
| Formule Amazon-like dans DistributionResult incohérente | Majeur | Snapshot / rapports trompeurs, risque contentieux vendeurs |
| Réconciliation “montants incohérents” faux positifs | Majeur | Indicateurs d’audit inutilisables |
| Pas d’idempotence sur RecordDistributionAsync | Moyen | Risque de double débit en cas de retry / job |
| Transferts utilisateur non audités | Moyen | Traçabilité incomplète |
| LastUpdated / UpdatedAt Wallet incohérents | Mineur | Confusion maintenance |
| PlatformProfit sans déduction ambassadeur | Mineur | Métriques surestimées |

---

## 4. Fichiers critiques à modifier en priorité

1. **Services/SupplierPaymentService.cs** – Utiliser le montant réel vendeur pour l’audit ; gérer la promo une seule fois ou par company.
2. **Services/PlatformTransactionAuditService.cs** – Idempotence sur la distribution ; débit = montant réel vendeur.
3. **Services/AmazonLikeDistributionService.cs** – Formule “vendeur fixe” (commission sur grossAmount, SupplierAmount = brut - commission base) ; PlatformProfit avec déduction ambassadeur.
4. **Pages/Payments/Payment.cshtml.cs** – Remplacer `RecordOrderRevenueAsync(int.Parse(TransactionId), ...)` par `RecordSubscriptionPaymentAsync(...)` pour le paiement plan ambassadeur.
5. **Pages/Users/Admin/FinancialReconciliation/Index.cshtml.cs** – Restreindre la comparaison de montants à la transaction OrderPayment pour chaque commande.

---

## 5. Réponses : couverture d’audit et rôle de la réconciliation

### 5.1 L’audit est-il bien fait pour tout ?

| Flux | Enregistré dans PlatformTransaction ? | Catégorie / Montant |
|------|----------------------------------------|----------------------|
| **Commandes payées** (client paie) | Oui | `OrderPayment`, montant = `Payment.Amount` (exact). Appelé depuis : Payment.cshtml.cs (Wallet), MonCashWebhookService, MonCashService, StripeService. |
| **Paiements vendeurs** (après livraison) | Oui | `SupplierPayment`, montant = montant réel versé au vendeur. Une ligne par (OrderId, CompanyId). |
| **Code promo** (coût marketing) | Oui | `PromoDiscount`, montant = remise. Enregistré **une seule fois par commande** (pas N× en multi-vendeurs). |
| **Commission ambassadeur** | Oui | `CommissionReferrer`, montant = commission. Une ligne par distribution (par vendeur concerné). |
| **Abonnements ambassadeur** (plan payé) | Oui | `SubscriptionPayment`, montant = montant payé. Enregistré depuis Payment.cshtml.cs. |
| **Dépôts / retraits utilisateur** (wallet) | Oui | `UserDeposit` / `UserWithdrawal` (+ `BankFees` si frais). SecureFinancialService + approbation virement (Approve.cshtml.cs). |
| **Transferts entre utilisateurs** | Oui (trace) | `ManualAdjustment`, montant 0, description Transfert A → B. |

**Conclusion :** L’audit couvre bien commandes payées, paiements vendeurs, codes promo, paiements ambassadeurs et mouvements wallet, avec montants exacts et catégories dédiées pour une visualisation claire dans **Transactions > Platform Transactions**.

### 5.2 FinancialReconciliation fait-il tout ce qu’il devrait ?

Oui. Après les corrections, la page :

1. **Revenus** : somme des Payment complétés (total + aujourd’hui).
2. **Couverture d’audit** : commandes = PT **OrderPayment** pour le même OrderId ; abonnements = PT **SubscriptionPayment** pour le même AmbassadorSubscriptionId (plus de faux positifs).
3. **Problèmes** : paiements sans audit, orphelins (OrderPayment/SubscriptionPayment sans Payment), incohérence de montant (OrderPayment uniquement), couverture &lt; 95 %, **écart solde wallet plateforme vs somme des PT**.
4. **Visualisation** : bloc Totaux d’audit (Revenus PT, Dépenses PT, Net, Solde plateforme, Cohérent/Écart) + lien vers Platform Transactions.
5. **Détail** : liste des paiements avec statut d’audit correct (commande vs abonnement).
6. **Par méthode de paiement** : % d’audit basé sur les paiements réellement audités (OrderPayment ou SubscriptionPayment).

---

*Rapport généré à partir de l’analyse du code source MCBusiness (ASP.NET Core 9, modèle financier Amazon-like).*
