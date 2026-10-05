# Modifications de securisation MonCash - 4 octobre 2026

## Lot applique

1. `MonCash:BaseUrl` est maintenant utilise par le service au lieu d'URL de production codees en dur.
2. Seuls les domaines HTTPS officiels sandbox et production sont acceptes.
3. La production exige l'activation explicite `MonCash:AllowProduction=true`.
4. Les payouts exigent separement `MonCash:EnablePayouts=true` et restent desactives par defaut.
5. Les retraits MonCash sont bloques avant tout debit tant que la refonte transactionnelle n'est pas terminee.
6. La page retrait exige `AmbassadorPolicy`.
7. Les endpoints `CustomerStatus`, `PrefundedTransactionStatus` et `PrefundedBalance` ont ete corriges.
8. Un client HTTP MonCash nomme possede maintenant un timeout de 15 secondes.
9. Les logs de token OAuth, reponses brutes, lien de paiement et details du payeur ont ete supprimes.
10. L'email de test declenche a chaque callback a ete supprime.
11. `/Payments/PaymentMethods` exige une authentification.
12. Le montant de depot est valide cote serveur entre 50 et 20 000 HTG, avec deux decimales maximum.
13. Une initiation echouee passe en `Failed` et produit un log redige au lieu de rester silencieusement pending.
14. Le calcul du brut MonCash est centralise dans `MonCashPricing`.
15. Le settlement exige le montant brut exact attendu.
16. Le callback exige un status 200, un message successful et un ID externe concordant.
17. Le callback HTTP `/api/moncash/callback` est maintenant effectivement mappe.
18. La page `ThankYou` est en lecture seule et ne credite plus un wallet via GET.
19. Le poller ne marque plus arbitrairement un paiement pending comme echoue apres une heure.
20. Les erreurs HTTP MonCash conservent leur status code.
21. `Transaction.OrderId` possede un index unique filtre via la migration `MonCashOrderIdUniqueness`.
22. La redirection post-retrait a ete corrigee.

## Configuration sure par defaut

```text
MonCash__BaseUrl=https://sandbox.moncashbutton.digicelgroup.com
MonCash__AllowProduction=false
MonCash__EnablePayouts=false
```

Ne pas activer `EnablePayouts` avant la mise en place du workflow reserve/pending/unknown/reconciliation decrit dans l'audit.

## Verification

- Restauration NuGet : reussie.
- Compilation Release : reussie.
- Resultat : 0 erreur, 199 avertissements historiques/restants.
- Migration generee : `20261005004745_MonCashOrderIdUniqueness`.

## Verification obligatoire avant migration de la base

Executer une recherche de doublons avant le deploiement :

```sql
SELECT "OrderId", COUNT(*)
FROM "Transactions"
WHERE "OrderId" IS NOT NULL
GROUP BY "OrderId"
HAVING COUNT(*) > 1;
```

Si cette requete retourne des lignes, ne pas supprimer ni fusionner automatiquement les transactions. Elles doivent etre rapprochees avec MonCash avant d'appliquer l'index unique.

## Travail restant avant reactivation des retraits

1. Ajouter les entites `PayoutAttempt` et `PaymentAttempt`.
2. Reserver les fonds sans marquer le retrait termine avant l'appel externe.
3. Ajouter l'etat `Unknown` pour les reponses reseau ambigues.
4. Rapprocher un payout ambigu via sa reference avant remboursement.
5. Persister l'ID MonCash, le montant, le receiver masque et le status externe.
6. Unifier principal, frais client, frais fournisseur et impact comptable.
7. Ajouter des tests de concurrence, rejeu, timeout et crash.
