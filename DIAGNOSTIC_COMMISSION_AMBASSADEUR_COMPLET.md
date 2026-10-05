# 🔍 DIAGNOSTIC COMPLET - COMMISSIONS AMBASSADEUR

## ✅ RÉSUMÉ DE L'ANALYSE

**Architecture vérifiée :**
- ✅ Middleware AmbassadorTracking enregistré (Program.cs ligne 393)
- ✅ Services injectés correctement
- ✅ Intégration PlaceOrder présente (ligne 294)
- ✅ Calcul commission après livraison présent (OrderStatusService ligne 69)

---

## 📋 WORKFLOW COMPLET (ÉTAPE PAR ÉTAPE)

### ÉTAPE 1: Clic sur lien ambassadeur ✅
**URL:** `?ref=CODE123&company=5`

**Fichier:** `Middlewares/AmbassadorTrackingMiddleware.cs`

**Ce qui se passe:**
1. Ligne 25: Détection paramètre `ref`
2. Ligne 39: Appel `ProcessAmbassadorReferralAsync()`
3. Lignes 68-82: Validation ambassadeur (1 seule requête SQL optimisée)
4. Lignes 173-182: **Création 5 cookies** (durée 7 jours)
5. Lignes 148-149: **Création 2 sessions**

**Cookies créés:**
- `ambassador_ref` = CODE123
- `ambassador_ref_id` = USER_ID
- `ambassador_ref_timestamp` = NOW
- `ambassador_ref_expiry` = NOW + 7 jours
- `ambassador_company_id` = 5 (optionnel)

**Sessions créées:**
- `AmbassadorReferralCode` = CODE123
- `AmbassadorReferralId` = USER_ID
- `AmbassadorCompanyId` = 5 (optionnel)

**Log attendu:**
```
Ambassador referral tracked: CODE123 for user XXX (Company: 5) - Expire le DD/MM/YYYY
```

**🔴 POINTS DE VÉRIFICATION:**
```sql
-- 1. Vérifier qu'un ambassadeur actif existe
SELECT 
    u.Id, 
    u.UserName, 
    u.ReferralCode,
    s.Status as SubscriptionStatus
FROM AspNetUsers u
JOIN AspNetUserRoles ur ON u.Id = ur.UserId
JOIN AspNetRoles r ON ur.RoleId = r.Id
LEFT JOIN AmbassadorSubscriptions s ON s.UserId = u.Id AND s.Status = 'Active'
WHERE r.Name = 'Ambassador'
  AND u.ReferralCode IS NOT NULL
  AND s.Id IS NOT NULL
LIMIT 5;
```

**Si ÉCHEC possible:**
- Ambassadeur sans `ReferralCode` (NULL)
- Pas d'abonnement `Active`
- Company inexistante
- Middleware non déclenché (logs vides)

---

### ÉTAPE 2: Inscription/Connexion ✅
**Fichier:** `Pages/Account/SignUp.cshtml.cs` (CORRIGÉ - n'écrit plus dans Referrals)

**Ce qui se passe:**
1. Utilisateur s'inscrit ou se connecte
2. **Cookies persistent automatiquement** (navigateur)
3. Session peut expirer mais sera restaurée depuis cookies

**🔴 POINTS DE VÉRIFICATION:**
- DevTools > Application > Cookies : Vérifier que les 5 cookies existent APRÈS connexion
- Si cookies disparus = problème de configuration CookiePolicy

---

### ÉTAPE 3: Création commande ✅
**Fichier:** `Pages/PlaceOrder.cshtml.cs` ligne 294

**Ce qui se passe:**
1. Ligne 294: `AssociateOrderWithAmbassadorAsync()` appelé
2. **Fichier:** `Services/OrderAmbassadorIntegrationService.cs`
3. Ligne 38: Lecture cookie via `GetAmbassadorReferralCode()`
4. Ligne 47: Validation 7 jours (pas 30 jours!)
5. Ligne 54: Recherche ambassadeur par ReferralCode
6. Ligne 64: Vérification anti-fraude (pas d'auto-référence)
7. Ligne 72: Vérification abonnement actif
8. Ligne 80: **Attribution `order.ReferredByAmbassador = CODE123`**
9. Ligne 83: Sauvegarde

**Log attendu:**
```
Order {ID} associated with ambassador {USER_ID} (code: CODE123)
```

**🔴 POINTS DE VÉRIFICATION:**
```sql
-- Vérifier attribution ambassadeur
SELECT 
    Id, 
    UserId, 
    ReferredByAmbassador, 
    Status, 
    TotalAmount, 
    CreatedAt
FROM Orders
WHERE ReferredByAmbassador IS NOT NULL
ORDER BY CreatedAt DESC
LIMIT 10;
```

**Si `ReferredByAmbassador` = NULL:**
- Cookies expirés (> 7 jours)
- Exception dans `AssociateOrderWithAmbassadorAsync()` (vérifier logs)
- Ambassadeur sans abonnement actif
- Auto-référence détectée

---

### ÉTAPE 4: Paiement commande ✅
**Fichier:** `Pages/Payments/Payment.cshtml.cs`

**Ce qui se passe:**
1. Utilisateur paie via Wallet/MonCash/Stripe
2. `OrderStatus` passe à `Paid`
3. **AUCUNE commission créée à ce stade** (c'est normal!)

**Pourquoi ?**
- Les commissions sont créées lors de la **LIVRAISON**, pas du paiement
- Raison: Éviter commissions sur commandes annulées/remboursées

---

### ÉTAPE 5: Livraison commande ✅
**Fichier:** `Services/OrderStatusService.cs` ligne 69

**Ce qui se passe:**
1. Admin marque produits comme `Delivered`
2. Ligne 56: `DetermineOrderStatus()` calcule nouveau statut
3. Si tous produits livrés → `OrderStatus.Delivered`
4. Ligne 65: **Détection changement vers Delivered**
5. Ligne 69: `ProcessCommissionsForDeliveredOrderAsync()` appelé

**Fichier:** `Services/OrderAmbassadorIntegrationService.cs` ligne 100

6. Ligne 103: Chargement commande
7. Ligne 114: Vérification `ReferredByAmbassador` non NULL
8. Ligne 121: **Protection idempotence** (évite doublons)
9. Ligne 130: Vérification statut `Delivered`
10. Ligne 137: **`CalculateCommissionsAsync()` appelé**

**Fichier:** `Services/AmbassadorCommissionService.cs` ligne 35

11. Calcul commissions par vendeur
12. Création objets `AmbassadorCommission`
13. Statut = `Pending`

**Log attendu:**
```
Successfully processed X commissions for order {ID}
Commission created: 50 HTG for ambassador {USER_ID}
```

**🔴 POINTS DE VÉRIFICATION:**
```sql
-- Vérifier création commissions
SELECT 
    ac.Id,
    ac.AmbassadorId,
    ac.OrderId,
    ac.Type,
    ac.Amount,
    ac.Status,
    ac.CreatedAt,
    o.ReferredByAmbassador,
    o.Status as OrderStatus
FROM AmbassadorCommissions ac
JOIN Orders o ON ac.OrderId = o.Id
ORDER BY ac.CreatedAt DESC
LIMIT 10;
```

**Si aucune commission:**
- `ReferredByAmbassador` = NULL à l'étape 3
- Statut commande ≠ `Delivered`
- Exception dans `CalculateCommissionsAsync()` (logs!)
- Commissions déjà créées (idempotence)

---

### ÉTAPE 6: Traitement automatique (Background) ✅
**Fichier:** `Services/AmbassadorCommissionProcessor.cs`

**Ce qui se passe:**
1. Toutes les 30 minutes
2. Traite commissions `Pending` → `Approved`
3. Puis `Approved` → `Released` (crédite wallet)

**🔴 POINTS DE VÉRIFICATION:**
```sql
-- Vérifier wallet crédité
SELECT 
    wt.Id,
    wt.UserId,
    wt.Amount,
    wt.Type,
    wt.Description,
    wt.CreatedAt
FROM WalletTransactions wt
WHERE wt.Type = 'Commission'
ORDER BY wt.CreatedAt DESC
LIMIT 10;
```

---

## 🔴 PROBLÈMES IDENTIFIÉS ET HYPOTHÈSES

### Problème #1: Cookies non créés
**Symptôme:** Aucun cookie `ambassador_ref` dans DevTools

**Causes possibles:**
1. ❌ URL mal formée (manque `?ref=CODE`)
2. ❌ Ambassadeur sans `ReferralCode`
3. ❌ Abonnement ambassadeur expiré
4. ❌ Company ID invalide (si fourni)
5. ❌ Exception silencieuse (vérifier logs)

**Test:**
```
http://localhost:3000/?ref=TESTCODE&company=1
```

Puis vérifier:
- DevTools > Console : Erreurs?
- Logs backend : "Ambassador referral tracked" ?

---

### Problème #2: Cookies supprimés après connexion
**Symptôme:** Cookies présents avant login, disparus après

**Causes possibles:**
1. ❌ `CookiePolicy` trop restrictive
2. ❌ Consentement cookies refusé
3. ❌ Durée de vie cookies mal configurée

**Vérification:**
```csharp
// Program.cs ligne 226-249
// Vérifier que les cookies ambassadeur ne sont PAS marqués comme nécessitant consentement
```

---

### Problème #3: ReferredByAmbassador = NULL
**Symptôme:** Commande créée mais sans référence ambassadeur

**Causes possibles:**
1. ❌ Cookies expirés (> 7 jours)
2. ❌ Exception dans `AssociateOrderWithAmbassadorAsync()`
3. ❌ Auto-référence détectée
4. ❌ Ambassadeur sans abonnement actif au moment de la commande

**Test SQL:**
```sql
-- Comparer Orders vs UserActivityLogs
SELECT 
    o.Id as OrderId,
    o.UserId,
    o.ReferredByAmbassador,
    ual.Details
FROM Orders o
LEFT JOIN UserActivityLogs ual ON ual.UserId = o.UserId 
    AND ual.Action = 'VisitedWithReferral'
    AND ual.Timestamp < o.CreatedAt
WHERE o.CreatedAt > NOW() - INTERVAL '7 days'
ORDER BY o.CreatedAt DESC;
```

---

### Problème #4: Commissions non créées
**Symptôme:** `ReferredByAmbassador` rempli mais aucune commission

**Causes possibles:**
1. ❌ Statut commande ≠ `Delivered`
2. ❌ Exception dans `CalculateCommissionsAsync()`
3. ❌ Pas de produits éligibles
4. ❌ `OrderStatusService` non appelé

**Test:**
```sql
-- Commandes livrées sans commissions
SELECT o.*
FROM Orders o
LEFT JOIN AmbassadorCommissions ac ON ac.OrderId = o.Id
WHERE o.ReferredByAmbassador IS NOT NULL
  AND o.Status = 'Delivered'
  AND ac.Id IS NULL;
```

---

## 🔧 DIAGNOSTIC EN 5 MINUTES

**Étape 1: Tester création cookies**
```bash
# Terminal
curl -v "http://localhost:3000/?ref=TESTCODE123" 2>&1 | grep "Set-Cookie"
```

Résultat attendu:
```
Set-Cookie: ambassador_ref=TESTCODE123; expires=...
Set-Cookie: ambassador_ref_id=XXX; expires=...
```

**Étape 2: Vérifier ambassadeur actif**
```sql
SELECT u.ReferralCode, s.Status
FROM AspNetUsers u
JOIN AmbassadorSubscriptions s ON s.UserId = u.Id
WHERE u.ReferralCode = 'TESTCODE123';
```

Résultat attendu:
```
ReferralCode | Status
-------------|-------
TESTCODE123  | Active
```

**Étape 3: Créer commande de test**
1. Cliquer lien `?ref=TESTCODE123`
2. Se connecter
3. Ajouter produit au panier
4. Créer commande
5. Vérifier:
```sql
SELECT Id, ReferredByAmbassador FROM Orders ORDER BY Id DESC LIMIT 1;
```

**Étape 4: Marquer comme livré**
```sql
UPDATE Orders SET Status = 'Delivered' WHERE Id = XXX;
```

Attendre 30 secondes, puis:
```sql
SELECT * FROM AmbassadorCommissions WHERE OrderId = XXX;
```

---

## 📊 CHECKLIST COMPLÈTE

| Étape | Vérification | Commande/Outil | Résultat Attendu |
|-------|--------------|----------------|------------------|
| 1 | Middleware actif | Logs backend | "AmbassadorTrackingMiddleware" présent |
| 2 | Cookies créés | DevTools > Cookies | 5 cookies `ambassador_*` |
| 3 | Session active | - | - (non vérifiable) |
| 4 | Ambassadeur actif | SQL ambassadeurs | `ReferralCode` + `Status=Active` |
| 5 | Ordre créé | SQL orders | `ReferredByAmbassador` rempli |
| 6 | Ordre livré | SQL orders | `Status = Delivered` |
| 7 | Commissions créées | SQL commissions | Lignes présentes |
| 8 | Wallet crédité | SQL wallet | `Type = Commission` |

---

## 🚨 ACTIONS IMMÉDIATES

**Sans modifier le code, faites:**

1. **Activer logs détaillés** (appsettings.Development.json):
```json
{
  "Logging": {
    "LogLevel": {
      "MCBusiness.Middlewares.AmbassadorTrackingMiddleware": "Debug",
      "MCBusiness.Services.OrderAmbassadorIntegrationService": "Debug",
      "MCBusiness.Services.AmbassadorCommissionService": "Debug"
    }
  }
}
```

2. **Tester avec lien complet:**
```
http://localhost:3000/?ref=<CODE_REEL>&company=<ID_COMPANY>
```

3. **Surveiller logs en temps réel:**
```bash
tail -f logs/mcbusiness.log | grep -i "ambassador"
```

4. **Exécuter tests SQL** (ci-dessus)

---

## 📝 RAPPORT À ME FOURNIR

Pour identifier le problème exact, donnez-moi:

1. **Logs lors du clic sur lien:**
   - Y a-t-il "Ambassador referral tracked" ?
   - Y a-t-il des erreurs/warnings ?

2. **Cookies dans DevTools:**
   - Screenshot de Application > Cookies
   - Sont-ils présents AVANT login ?
   - Sont-ils présents APRÈS login ?

3. **Résultat SQL:**
```sql
-- Ambassadeur de test
SELECT u.Id, u.ReferralCode, s.Status 
FROM AspNetUsers u 
JOIN AmbassadorSubscriptions s ON s.UserId = u.Id 
WHERE u.ReferralCode = '<VOTRE_CODE>';

-- Dernière commande
SELECT Id, UserId, ReferredByAmbassador, Status, CreatedAt 
FROM Orders 
ORDER BY Id DESC LIMIT 1;

-- Commissions
SELECT * FROM AmbassadorCommissions 
ORDER BY Id DESC LIMIT 5;
```

4. **Quelle étape échoue?**
   - Cookies non créés ?
   - ReferredByAmbassador NULL ?
   - Commissions non créées ?
   - Wallet non crédité ?
