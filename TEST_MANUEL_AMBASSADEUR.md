# 🧪 TEST MANUEL DU SYSTÈME AMBASSADEUR

## 📋 PRÉREQUIS

Avant de commencer, assurez-vous d'avoir :
1. ✅ Application en cours d'exécution (`dotnet watch`)
2. ✅ Un compte ambassadeur actif avec un code de référence
3. ✅ Navigateur avec DevTools ouvert (F12)

---

## 🚀 ÉTAPE 1 : Obtenir votre code ambassadeur

### Option A - Si vous avez déjà un ambassadeur

Connectez-vous avec un compte ambassadeur et allez sur :
```
http://localhost:5000/Users/AmbassadorSpace/AmbassadorLinks
```

Copiez votre **code de référence** (ex: `AMBASSADEUR123`)

### Option B - Créer un ambassadeur rapidement via SQL

```sql
-- 1. Trouvez un utilisateur existant
SELECT "Id", "UserName", "Email", "ReferralCode" 
FROM "AspNetUsers" 
WHERE "IsAmbassador" = true 
LIMIT 1;

-- Si aucun, transformez un utilisateur en ambassadeur :
UPDATE "AspNetUsers" 
SET "IsAmbassador" = true, 
    "ReferralCode" = 'TEST123'
WHERE "UserName" = 'VOTRE_USERNAME';

-- 2. Créez un plan ambassadeur actif (si inexistant)
INSERT INTO "AmbassadorPlans" 
("Name", "Price", "DurationInDays", "IsActive", "AmbassadorCommissionPercentage", "CreatedAt")
VALUES 
('Plan Test', 0, 30, true, 10.0, NOW())
RETURNING "Id";

-- 3. Créez une souscription active
INSERT INTO "AmbassadorSubscriptions"
("UserId", "PlanId", "StartDate", "EndDate", "Status", "CreatedAt")
VALUES
('ID_USER_ICI', ID_PLAN_ICI, NOW(), NOW() + INTERVAL '30 days', 'Active', NOW());
```

---

## 🔗 ÉTAPE 2 : Tester le lien ambassadeur

### A. Ouvrez une fenêtre de navigation privée (Ctrl+Shift+N)

### B. Collez cette URL (remplacez les valeurs) :

```
http://localhost:5000/?ref=TEST123&company=1
```

**Remplacez :**
- `TEST123` → Votre code de référence ambassadeur
- `1` → ID d'une compagnie existante (optionnel)

### C. **REGARDEZ LA CONSOLE dotnet watch** 

Vous devriez voir apparaître :
```
warn: [DIAG-TEMP] MIDDLEWARE: Détecté ref=TEST123, company=1, URL=/?ref=TEST123&company=1
warn: [DIAG-TEMP] MIDDLEWARE: Cookies créés - ref=TEST123, ambassadorId=xxx-xxx, expiry=2026-03-01, hasCompany=True
```

✅ **Si vous voyez ces logs** : Les cookies sont créés !  
❌ **Si vous ne voyez rien** : Le middleware ne détecte pas les paramètres

---

## 🍪 ÉTAPE 3 : Vérifier les cookies dans le navigateur

### A. Ouvrez DevTools (F12) → Onglet **Application** → **Cookies** → `http://localhost:5000`

### B. Vérifiez que ces cookies existent :

| Cookie | Valeur attendue |
|--------|----------------|
| `ambassador_ref` | `TEST123` |
| `ambassador_ref_id` | `xxx-xxx-xxx` (UUID) |
| `ambassador_ref_timestamp` | Date ISO |
| `ambassador_ref_expiry` | Date + 7 jours |
| `ambassador_company_id` | `1` (si fourni) |

✅ **Si les cookies sont là** : Middleware fonctionne !  
❌ **Si aucun cookie** : Problème dans le middleware

---

## 📝 ÉTAPE 4 : S'inscrire ou se connecter

### A. Depuis la même fenêtre privée, créez un nouveau compte :

```
http://localhost:5000/Account/SignUp
```

**Remplissez le formulaire et inscrivez-vous.**

### B. **REGARDEZ LA CONSOLE dotnet watch**

Vous devriez voir :
```
info: [DIAG-TEMP] ... (pas de log ici car on n'enregistre plus dans Referrals)
```

### C. **VÉRIFIEZ QUE LES COOKIES PERSISTENT**

Retournez dans DevTools → Cookies → Les 5 cookies doivent toujours être là !

✅ **Cookies présents** : Bon !  
❌ **Cookies disparus** : Problème de persistance session

---

## 🛒 ÉTAPE 5 : Passer une commande

### A. Ajoutez un produit au panier

```
http://localhost:5000/Shop
```

Choisissez un produit et ajoutez-le au panier.

### B. Passez la commande

```
http://localhost:5000/PlaceOrder
```

Remplissez l'adresse et créez la commande.

### C. **REGARDEZ LA CONSOLE dotnet watch**

Vous devriez voir :
```
warn: [DIAG-TEMP] ORDER-INTEGRATION: Lecture cookies - ref=TEST123, orderId=XXX
warn: [DIAG-TEMP] ORDER-INTEGRATION: Attribution réussie - orderId=XXX, ref=TEST123, ambassadorId=xxx-xxx
```

✅ **Si vous voyez ces logs** : Commande associée à l'ambassadeur !  
❌ **Si "ref=NULL"** : Cookies perdus ou non lus

---

## 💳 ÉTAPE 6 : Payer la commande

### A. Payez la commande avec votre méthode de paiement

Allez sur la page de paiement et complétez le paiement.

### B. Changez le statut de la commande en "Delivered" (SQL)

```sql
-- Trouvez votre commande
SELECT "Id", "UserId", "ReferredByAmbassador", "Status", "TotalAmount"
FROM "Orders"
ORDER BY "OrderDate" DESC
LIMIT 5;

-- Changez le statut en Delivered
UPDATE "Orders"
SET "Status" = 'Delivered'
WHERE "Id" = VOTRE_ORDER_ID;
```

### C. **ATTENDEZ 30 SECONDES** (le background service s'exécute toutes les 30s)

### D. **REGARDEZ LA CONSOLE dotnet watch**

Vous devriez voir :
```
warn: [DIAG-TEMP] COMMISSION-SERVICE: Début calcul - orderId=XXX, ref=TEST123, status=Delivered
warn: [DIAG-TEMP] COMMISSION-SERVICE: Commission créée - orderId=XXX, ambassadorId=xxx-xxx, amount=50.00
```

✅ **Commission créée** : TOUT FONCTIONNE ! 🎉  
❌ **Aucune commission** : Problème dans le calcul

---

## 🔍 ÉTAPE 7 : Vérifier la commission en base de données

```sql
SELECT 
    ac."Id",
    ac."OrderId",
    ac."AmbassadorId",
    ac."Amount",
    ac."Status",
    ac."Type",
    ac."CreatedAt",
    o."ReferredByAmbassador"
FROM "AmbassadorCommissions" ac
JOIN "Orders" o ON ac."OrderId" = o."Id"
ORDER BY ac."CreatedAt" DESC
LIMIT 5;
```

✅ **Commission présente** : Système complet fonctionnel !  
❌ **Aucune commission** : Copiez-moi tous les logs et résultats SQL

---

## 📊 ÉTAPE 8 : Me donner les résultats

### Copiez-collez dans votre réponse :

1. **Tous les logs `[DIAG-TEMP]`** de la console
2. **Screenshot des cookies** (DevTools)
3. **Résultat SQL** de la dernière requête
4. **À quelle étape ça bloque** (1-7)

Avec ces infos, je saurai EXACTEMENT où corriger !

---

## ⚠️ PROBLÈMES COURANTS

| Symptôme | Cause probable | Solution |
|----------|---------------|----------|
| Aucun log `[DIAG-TEMP]` | Middleware pas activé | Vérifier `Program.cs` ligne 393 |
| Cookies non créés | Ambassadeur inexistant/inactif | Vérifier SQL étape 1 |
| ref=NULL lors commande | Cookies supprimés | Vérifier DevTools cookies |
| Aucune commission | Statut ≠ Delivered | Mettre `Status = 'Delivered'` |
| Commission=0 | Produits non éligibles | Vérifier `CompanyId` des produits |

---

**Faites le test et donnez-moi les résultats ! 🚀**
