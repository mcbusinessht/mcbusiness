# Audit de securite et d'integrite du systeme MonCash

Date : 4 octobre 2026  
Perimetre : depot MonCash, retour navigateur, pseudo-webhook, poller de statut, retraits prefundes, configuration, journalisation et ecritures financieres.  
Verdict : **NO-GO pour l'argent reel**.

## Resume executif

Le systeme contient de bons debuts d'idempotence en base (transaction serialisable, index unique sur l'identifiant externe), mais l'orchestration globale n'est pas sure. Les trois defauts dominants sont :

1. le webhook declare n'est expose par aucune route;
2. les retraits sont comptabilises comme termines avant que MonCash ne confirme le transfert;
3. la configuration sandbox/production est ignoree au profit d'URL de production codees en dur.

Ces defauts peuvent produire des clients debites sans paiement, des paiements MonCash suivis d'un remboursement local, des transactions payees mais jamais creditees et des ecarts comptables impossibles a rapprocher automatiquement.

## P0 - Bloquants critiques

### P0-1 - Retrait : debit local avant appel MonCash

**Chemin actuel**

1. `MonCash.cshtml.cs:120-130` calcule `totalAmount = montant + 3%`.
2. `ProcessWithdrawalAsync` debite immediatement le wallet, marque la transaction `Completed` et commit (`SecureFinancialService.cs:83-155`).
3. Seulement ensuite, l'application appelle `SendPayoutAsync` (`MonCash.cshtml.cs:135-137`).

**Scenarios dangereux**

- Crash apres le commit local mais avant l'appel MonCash : wallet debite, destinataire non paye.
- Timeout/reponse perdue apres que MonCash a execute le paiement : le `catch` rembourse localement (`:165-173`), mais le destinataire a potentiellement deja recu l'argent.
- Processus tue avant le remboursement : transaction locale reste `Completed` sans preuve de payout.

**Correction obligatoire**

- Etat initial `PayoutPending`, montant reserve et non definitivement debite.
- Reference idempotente unique creee avant l'appel.
- Appel MonCash.
- Si reponse ambigue, interroger `PrefundedTransactionStatus` avec la reference; ne jamais rembourser automatiquement tant que le statut externe est inconnu.
- Finaliser le debit seulement apres confirmation externe; sinon liberer la reservation.
- Reconciliation periodique de tous les statuts `Pending/Unknown`.

### P0-2 - Double avantage possible : payout reussi + remboursement wallet

Le code considere toute exception ou reponse non exactement egale a `successful` comme un echec et appelle `ReverseWithdrawalAsync`. Une erreur reseau ne prouve pas que MonCash a echoue.

**Impact** : le client peut recevoir le payout MonCash puis recuperer simultanement son solde MC Business.  
**Correction** : distinguer `Failed` de `Unknown`; pour `Unknown`, verifier par reference jusqu'a un etat terminal. Le remboursement n'est permis que sur echec externe confirme.

### P0-3 - Le webhook n'existe pas reellement dans le pipeline HTTP

`MonCashWebhookService` est enregistre dans DI (`Program.cs:131`), mais aucune route ni aucun middleware n'appelle `HandleWebhookAsync`. La recherche du depot ne trouve aucun appel.

**Impact** : aucune notification serveur MonCash ne peut confirmer un paiement. Le systeme depend du retour navigateur et du poller, donc un utilisateur qui ferme sa fenetre peut rester non credite jusqu'au poller, voire definitivement en cas d'erreur.  
**Correction** : exposer un endpoint public strictement borne, documente et rate-limite; verifier chaque notification en relisant la transaction via l'API MonCash; repondre rapidement puis traiter via une file durable.

### P0-4 - Sandbox ignore, production toujours utilisee

`appsettings.json` et Compose fournissent `MonCash:BaseUrl`, mais `MonCashConfig` ne possede meme pas cette propriete. Toutes les methodes utilisent `ProductionUrl` (`MonCashService.cs:14`, `:94`, `:147`, `:247`, `:388`, `:450`).

**Impact** : tests pouvant toucher la production, credentials/environnements melanges et impossibilite d'un staging fiable.  
**Correction** : option typee `BaseUrl`, validation par environnement, liste blanche stricte des deux hosts, refus de demarrer si credentials et host ne correspondent pas au mode attendu.

### P0-5 - Depot accessible sans autorisation explicite et montant non valide cote serveur

`PaymentMethodsModel` n'a pas `[Authorize]` et `/Payments` n'est pas protege par convention. `OnPostMonCashPaymentAsync(decimal amount)` utilise le parametre brut; l'attribut `[Range]` appartient a `AmountBrut`, une autre propriete non utilisee.

**Impact** : creation de transactions anonymes/nulles ou montants negatifs, nuls, extremes ou a precision inattendue; pollution de base et appels fournisseur abusifs. Les bornes HTML ne sont pas une protection.  
**Correction** : `[Authorize]`, DTO `[BindProperty]` valide, `ModelState`, montant decimal positif avec bornes et deux decimales, utilisateur exige, rate limiting par compte et transaction creee via un service metier atomique.

### P0-6 - Une transaction payee peut etre marquee en echec apres une heure

Le poller marque toutes les transactions pending de plus d'une heure `Failed` (`TransactionStatusUpdater.cs:113-130`). Or un paiement peut etre confirme tardivement ou l'API peut etre temporairement indisponible. `CompletePendingDepositAsync` refuse ensuite tout statut different de `Pending` (`SecureFinancialService.cs:615`).

**Impact** : argent encaisse chez MonCash mais wallet jamais credite.  
**Correction** : ne jamais transformer l'absence de confirmation en echec. Employer `Expired/Unknown`, continuer la reconciliation et ne passer a `Failed` que si MonCash le confirme explicitement.

## P1 - Risques eleves

### P1-1 - Trois chemins concurrents de confirmation

Le credit peut etre tente par :

- la page GET `ThankYou`;
- le poller `TransactionStatusUpdater`;
- le service webhook, s'il est un jour branche.

Le service central utilise une transaction serialisable, ce qui limite le double credit, mais l'architecture multiplie les courses, appels externes et comportements divergents.

**Correction** : tous les producteurs publient un evenement ou appellent une seule commande idempotente `SettleDeposit`. La page retour ne modifie jamais les donnees; elle affiche seulement un statut.

### P1-2 - Etat modifie par une requete GET

`ThankYou.OnGetAsync` appelle directement `CompletePendingDepositAsync` (`ThankYou.cshtml.cs:43-89`). Un GET ne doit pas produire une ecriture financiere.

**Impact** : declenchement par prefetch, scanner, lien ou requete repetee; semantique HTTP et protection CSRF inappropriees.  
**Correction** : endpoint de notification serveur ou job; page GET en lecture seule.

### P1-3 - Journalisation de secrets et donnees sensibles

- Token d'acces imprime explicitement dans `MonCashService.cs:372`.
- Reponses API brutes imprimees aux lignes `117`, `164`, `264`, `339`, `405`.
- URL de paiement, contenant le token de redirection, imprimee dans `PaymentMethods.cshtml.cs:113`.
- Webhook journalise l'objet paiement complet, incluant le payeur (`MonCashWebhookService.cs:82`).
- Un email personnel en dur est contacte a chaque notification (`:45`).

**Impact** : vol de bearer token, fuite du numero payeur, de references et de donnees transactionnelles.  
**Correction** : supprimer immediatement ces sorties, redaction centralisee, correlation ID interne, acces/retention limites et rotation des secrets si ces logs ont quitte la machine de developpement.

### P1-4 - Identifiant `OrderId` non unique en base

L'identifiant est genere puis verifie par requete, mais aucun index unique sur `Transaction.OrderId` n'est configure. Deux requetes concurrentes peuvent obtenir la meme reference.

**Impact** : `RetrievePaymentByOrderId` peut retourner un paiement ambigu et `FirstOrDefaultAsync` peut crediter le mauvais depot.  
**Correction** : index unique filtre sur `OrderId`, generation cryptographique/UUID, gestion explicite du conflit unique.

### P1-5 - Verification insuffisante des donnees retournees

Le settlement verifie la reference et accepte `confirmedAmount >= deposit.Amount`; il ne verifie pas :

- egalite avec le montant brut reellement demande a MonCash;
- devise;
- mode sandbox/live;
- payer attendu si applicable;
- age/horodatage;
- `PaymentDetails.Status == 200` dans tous les chemins;
- concordance entre `Payment.TransactionId` et l'ID demande dans le webhook.

Le depot stocke seulement le montant net alors que MonCash recoit net + 2%, rendant le rapprochement du brut/frais incomplet.

**Correction** : persister `RequestedNet`, `ExpectedFee`, `ExpectedGross`, devise et mode; comparer strictement les champs attendus avant credit.

### P1-6 - Erreurs HTTP detruites, logique 404 inoperante

`RetrievePaymentByOrderIdAsync` capture `HttpRequestException`, puis la remplace par une `Exception` generique. Le `catch (HttpRequestException ... 404)` du poller ne peut donc pas fonctionner comme prevu.

**Impact** : mauvaise classification, absence de retry controle, transactions laissees dans un etat incoherent.  
**Correction** : conserver le type, status code et exception interne; definir erreurs `NotFound`, `Transient`, `Rejected`, `Malformed`.

### P1-7 - Retrait sans preuve externe persistante

Apres un payout reussi, `Transfer.TransactionId`, le receiver, le montant confirme, la reference et la reponse externe ne sont pas enregistres dans la transaction de retrait. La reponse disparait apres l'affichage.

**Impact** : rapprochement et traitement des litiges pratiquement impossibles.  
**Correction** : table `PaymentAttempt/PayoutAttempt` append-only avec reference interne, ID MonCash, statut, montant, numero masque, timestamps, compteur d'essais et payload minimal redige.

### P1-8 - Calcul de frais et comptabilite incoherents

La page debite `montant + 3%`, puis `ProcessWithdrawalAsync` recalcule des frais MonCash de 2% sur ce total. `RecordUserWithdrawalAsync` diminue le portefeuille plateforme de `amount + bankFees` (`PlatformTransactionAuditService.cs:714-748`). Le payout externe ne porte pourtant que sur le montant initial.

**Impact** : double comptabilisation de frais et ecart entre wallet client, ledger plateforme et mouvement MonCash.  
**Correction** : definir explicitement `principal`, `customerFee`, `providerFee`, `netPlatformImpact`; une seule fonction de tarification versionnee et des ecritures en partie double testees.

### P1-9 - Controle d'acces incomplet sur les retraits

La page retrait n'a ni `[Authorize]` ni `AmbassadorPolicy`. Elle recherche seulement l'utilisateur courant et cree/charge un wallet `Earnings`.

**Impact** : tout compte authentifie disposant d'un solde earnings pourrait tenter ce parcours, independamment du statut ambassadeur/abonnement.  
**Correction** : policy par role et droit metier actif, controle reexecute dans le service de retrait.

### P1-10 - Statut de succes compare par chaine fragile

Le code accepte uniquement `Message == "successful"`, avec casse exacte, parfois sans verifier le status HTTP metier. Pour les payouts, il ne compare pas le montant et le receiver de la reponse a la requete.

**Correction** : parseur unique, validation de schema et de tous les invariants; tests contractuels avec reponses reelles anonymisees.

## P2 - Fiabilite et exploitation

### P2-1 - Endpoints prefundes incorrects

D'apres la documentation REST officielle fournie par MonCash :

- le statut est `/Api/v1/PrefundedTransactionStatus`, alors que le code appelle `/v1/PrefundedTransactionStatusMonCash`;
- le solde est sous le host REST `/Api/v1/PrefundedBalance`, alors que le code construit `/v1/PrefundedBalance`;
- `CustomerStatus` souffre du meme oubli de `/Api`.

Ces methodes sont aujourd'hui peu ou pas utilisees, mais celle de statut est justement indispensable pour resoudre les payouts ambigus.

### P2-2 - Pas de client HTTP MonCash configure

`AddHttpClient()` est generique; chaque methode cree un client sans timeout MonCash explicite, politique de retry, circuit breaker, limite de taille de reponse ou instrumentation.

**Correction** : client nomme/type, timeout court, retry uniquement pour operations idempotentes, aucune repetition aveugle d'un payout, correlation et metriques.

### P2-3 - Token OAuth redemande pour chaque appel

Le token annonce une expiration courte dans la documentation, mais il est regenere a chaque operation. Cela augmente latence et dependance a l'endpoint OAuth.

**Correction** : cache concurrent jusqu'a quelques secondes avant expiration, sans jamais journaliser le token.

### P2-4 - Exceptions avalees lors de la creation du depot

`PaymentMethods.cshtml.cs:116-119` intercepte toute exception sans journaliser ni marquer la tentative en echec. Une transaction pending orpheline reste en base.

**Correction** : statut `InitiationFailed`, message utilisateur neutre, log structure redige et reconciliation/expiration controlee.

### P2-5 - Poller non scalable

Toutes les transactions pending sont chargees toutes les deux minutes sans pagination ni verrou de prise en charge. Chaque replica executera la meme boucle.

**Correction** : lots bornes, `NextAttemptAt`, backoff, nombre maximal d'essais, verrou distribue/`SKIP LOCKED` ou file durable, metriques de backlog.

### P2-6 - Mauvaise redirection apres retrait

`RedirectToPage("./Transaction")` depuis le dossier `Withdrawals` cible une page inexistante. La page Transaction connue est ailleurs.

### P2-7 - Message d'erreur technique expose au client

Le retrait affiche `ex.Message` a l'utilisateur (`MonCash.cshtml.cs:176`), pouvant reveler des details fournisseur ou internes.

### P2-8 - Webhook avec effet secondaire avant validation

L'email de test est envoye avant le bloc `try` et avant toute validation. Un appel abusif peut declencher du spam et une panne SMTP peut empecher le traitement du paiement.

## Points positifs a conserver

- Le detail d'un paiement est relu cote serveur aupres de MonCash; le parametre de retour n'est pas cru seul.
- `CompletePendingDepositAsync` utilise une transaction serialisable.
- Un index unique filtre existe sur `Transaction.TransactionId`.
- Le service detecte la reutilisation d'un ID de transaction externe.
- Le credit applique le montant interne attendu plutot que le montant envoye arbitrairement par le navigateur.
- `ReverseWithdrawalAsync` possede une reference de reversal deterministe, ce qui donne une base d'idempotence.
- Le formulaire retrait utilise antiforgery, mot de passe et reCAPTCHA.

Ces protections sont utiles, mais elles ne compensent pas les ruptures d'orchestration decrites plus haut.

## Architecture cible recommandee

### Depot

1. Requete authentifiee avec montant valide.
2. Creation atomique d'une `PaymentAttempt` unique contenant net, frais, brut, devise et utilisateur.
3. Creation du paiement MonCash avec cette reference.
4. Redirection utilisateur.
5. Return URL en lecture seule : affiche `Pending`.
6. Notification/poller : relit le paiement via l'API officielle.
7. Commande unique `SettleDeposit(reference, externalId)` dans une transaction serialisable.
8. Contrainte unique sur reference interne et ID externe.

### Retrait

1. Verification policy, mot de passe, limites et velocity checks.
2. Reservation atomique du montant principal + frais.
3. Creation `PayoutAttempt(Pending)` avec reference unique.
4. Appel MonCash une seule fois.
5. Succes certain : finalisation de la reservation et enregistrement de l'ID externe.
6. Echec certain : liberation.
7. Resultat inconnu : aucun remboursement; reconciliation par reference jusqu'au statut terminal.

## Plan de correction priorise

### Lot 1 - Urgence, avant tout test avec argent reel

1. Forcer sandbox par configuration validee.
2. Supprimer tous les logs sensibles et l'email de test.
3. Desactiver temporairement les retraits MonCash.
4. Proteger `/Payments` et valider le montant serveur.
5. Ajouter index unique `OrderId`.

### Lot 2 - Refonte transactionnelle

1. Introduire `PaymentAttempt` et `PayoutAttempt`.
2. Refaire le workflow retrait avec reservation et statut `Unknown`.
3. Brancher l'endpoint de notification et rendre `ThankYou` read-only.
4. Centraliser le settlement idempotent.
5. Corriger frais et ledger.

### Lot 3 - Reconciliation et exploitation

1. Corriger les endpoints prefundes.
2. Poller borne avec backoff/verrou distribue.
3. Tableau de rapprochement : tentatives internes vs transactions MonCash.
4. Alertes sur `Unknown`, ecart montant, doublon et backlog.

### Lot 4 - Validation

Tests obligatoires :

- double clic creation;
- double callback;
- callback et poller simultanes;
- paiement confirme apres plus d'une heure;
- timeout avant/apres execution d'un payout;
- redemarrage a chaque etape;
- deux replicas concurrents;
- montant negatif, excessif et precision > 2 decimales;
- reference dupliquee;
- transaction externe reutilisee;
- mauvaise devise/montant/receiver;
- utilisateur non autorise;
- indisponibilite OAuth/API/DB.

## Criteres GO MonCash

- Aucun retrait ne peut conduire a `payout + remboursement`.
- Chaque mouvement MonCash est rapproche a une tentative interne unique.
- Double notification/retry ne produit qu'une seule ecriture wallet.
- Aucun paiement confirme n'est transforme en echec par expiration locale.
- Aucun token, numero complet ou payload fournisseur dans les logs.
- Sandbox et production sont impossibles a confondre.
- Tests de concurrence et de panne verts.
- Dashboard et alertes de reconciliation operationnels.

## Limites de l'audit

L'analyse est statique. Aucun compte marchand MonCash, historique de transactions, configuration Return URL/Alert URL ni environnement sandbox executable n'a ete fourni. La documentation MonCash publique consultee est datee; les endpoints et contrats doivent etre confirmes avec le portail marchand ou le support MonCash avant implementation finale.
