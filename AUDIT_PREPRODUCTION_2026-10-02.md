# Audit complet de preparation a la production - MC Business

Date de l'audit : 2 octobre 2026  
Perimetre : application ASP.NET Core/Razor Pages, logique metier, finance, paiements, securite, donnees, exploitation et deploiement.  
Nature : audit statique du depot et compilation locale. Aucun changement fonctionnel n'a ete applique.

## Verdict

**Decision recommandee : NO-GO production.**

Le projet compile en Release, mais il comporte plusieurs vulnerabilites directement exploitables, des risques d'integrite financiere, un conteneur qui ne peut pas demarrer tel qu'il est defini, aucune suite de tests et aucun dispositif de supervision/health check. Les problemes P0 et P1 ci-dessous doivent etre corriges et testes avant toute exposition publique ou manipulation d'argent reel.

## Controle realise

- Inventaire : environ 437 fichiers C# et 307 vues Razor.
- Compilation : `dotnet build MCBusiness.sln -c Release --no-restore` reussie.
- Resultat : **0 erreur, 193 avertissements**.
- Tests automatises : **aucun projet de tests trouve**.
- Audit NuGet : une vulnerabilite transitive haute et deux faibles detectees.
- Analyse ciblee : authentification, autorisation multi-vendeur, messagerie SignalR, paiements MonCash/Stripe, portefeuilles, commissions, retraits, uploads, taches de fond, migrations, Docker, secrets, logs, cache et observabilite.
- Limites : aucun environnement de staging ni base de production n'a ete fourni; les parcours n'ont donc pas ete testes bout en bout contre les passerelles reelles. Le dossier `.git` local est vide, ce qui empeche la revue de l'historique et l'identification fiable des fichiers suivis.

## P0 - Bloquants absolus

### P0-1 - Conteneur de production non demarrable

**Preuves**

- `MCBusiness.csproj:4` cible `net9.0`.
- `Dockerfile:2` et `Dockerfile:16` utilisent les images SDK/runtime .NET 8.
- `Dockerfile:24` lance `FriKob.dll`, alors que le projet produit `MCBusiness.dll`.

**Impact** : echec du build ou du demarrage du conteneur.  
**Correction** : aligner SDK/runtime sur .NET 9, lancer `MCBusiness.dll`, construire l'image en CI et effectuer un smoke test HTTP dans cette image.

### P0-2 - Rupture d'isolation entre vendeurs (IDOR / multi-tenant)

Le role `Seller` donne acces au dossier, mais de nombreux handlers chargent et modifient une entite uniquement par son identifiant, sans verifier que sa `Company` appartient au vendeur connecte.

**Exemples prouves**

- `Pages/Users/Seller/Products/Edit.cshtml.cs:25-33` charge n'importe quel produit par ID; le POST attache ensuite directement l'objet recu (`:94-101`).
- `Pages/Users/Seller/Products/Delete.cshtml.cs:48-92` supprime logiquement un produit et ses dependances sans controle de propriete.
- `Pages/Users/Seller/Products/ProductVariants/Edit.cshtml.cs:36-70` permet de modifier prix, quantite et disponibilite d'une variante par ID.
- `Pages/Users/Seller/Orders/UpdateOrderProductStatus.cshtml.cs:38-76` permet de changer le statut de n'importe quelle ligne de commande.
- `Pages/Users/Seller/Shipments/Index.cshtml.cs:42-69` liste toutes les expeditions de la plateforme, puis les actions par ID ne verifient pas le vendeur.
- `Pages/Users/Seller/DeliveryPersons/Edit.cshtml.cs:30-55` presente le meme schema.

**Impact** : un vendeur peut consulter ou alterer le catalogue, le stock, les commandes, livraisons et declenchements financiers d'un autre vendeur.  
**Correction** : centraliser un filtre de tenant (`SellerId -> CompanyId`) et l'appliquer dans chaque requete GET et POST. Ne jamais faire `Attach(boundEntity).Modified`; recharger l'entite autorisee puis copier une liste blanche de champs.

### P0-3 - Messagerie : lecture/ecriture dans les conversations d'autrui

**Preuves**

- `Hubs/ChatHub.cs` n'a pas `[Authorize]`.
- `JoinConversation` ajoute la connexion au groupe avant de verifier l'appartenance a la conversation.
- `SendTyping` envoie dans n'importe quel groupe sans controle d'appartenance.
- `SendMessage` insere un message pour n'importe quel `conversationId`, sans verifier les participants et sans renseigner `ReceiverId`.
- `Pages/Users/User/Conversations/Chat.cshtml.cs:81-85` charge une conversation par ID sans filtrer par utilisateur.
- Le POST de la meme page (`:195-210`) permet aussi d'envoyer un message sans verifier que l'expediteur est participant.

**Impact** : fuite de conversations, usurpation de messages, surveillance de presence et corruption des echanges.  
**Correction** : `[Authorize]` sur les hubs; verification d'appartenance avant tout `AddToGroupAsync`, envoi ou ecriture; filtrage par participant dans la page; tests d'autorisation croisee.

### P0-4 - Flux de depot MonCash insuffisamment valide

**Preuves**

- `Pages/Payments/PaymentMethods.cshtml.cs` n'est pas protege par `[Authorize]` ni par une convention sur `/Payments`.
- `OnPostMonCashPaymentAsync(decimal amount)` (`:83`) utilise directement le montant poste; l'attribut `Range` porte sur une autre propriete (`AmountBrut`) et n'est donc pas applique.
- Une transaction pending est creee avant l'appel externe (`:90-105`).
- Les exceptions sont avalees silencieusement (`:116-119`).
- Le serveur demande a MonCash `amount + 2%` (`:111`), tandis que la confirmation accepte tout montant confirme superieur ou egal au principal (`SecureFinancialService.cs:615`) sans egalite devise/frais/reference forte.
- La page de retour GET `ThankYou` peut declencher le credit (`ThankYou.cshtml.cs:43-89`), en plus du webhook et du poller d'arriere-plan.
- Le webhook envoie un email de test en dur a chaque appel (`MonCashWebhookService.cs:45`).

**Impact** : transactions orphelines, abus par requetes anonymes, ecarts de rapprochement, surface de rejeu et comportements divergents entre trois chemins de confirmation.  
**Correction** : exiger l'authentification; utiliser un DTO valide; generer montant/devise/frais cote serveur; conserver un identifiant de tentative unique; n'autoriser qu'un seul service idempotent de settlement; verifier montant exact, devise, destinataire, reference et statut aupres du fournisseur; laisser le webhook/poller confirmer, jamais la page de retour utilisateur.

### P0-5 - Jeton de reinitialisation de mot de passe expose dans les logs

**Preuve** : `Pages/Account/ResetPassword.cshtml.cs:52-53` journalise l'adresse email et le jeton de reinitialisation en clair.  
**Impact** : prise de controle de compte pour toute personne ou tout service ayant acces aux logs.  
**Correction** : supprimer immediatement ces logs, purger/faire expirer les journaux concernes et invalider les jetons potentiellement exposes.

## P1 - Critiques avant production

### P1-1 - Preuves bancaires publiques et validation de fichier faible

- Les justificatifs sont ecrits sous `wwwroot/uploads/bankproofs` (`PaymentMethods.cshtml.cs:137-172`) et sont donc servis publiquement par `UseStaticFiles`.
- Le controle repose sur la taille et l'extension du nom; la signature reelle du fichier et son type MIME ne sont pas verifies.

**Impact** : divulgation de donnees bancaires/personnelles et hebergement de contenu malveillant.  
**Correction** : stockage prive hors `wwwroot`, telechargement via endpoint autorise, nom serveur, verification magic bytes/MIME, antivirus, quotas et politique de retention.

### P1-2 - Cache public sur toutes les API et pages non classees

`Program.cs:459` applique `public, max-age=300` a toute URL `/api/`; `:470` applique un cache public aux autres pages. Des API authentifiees comme `/api/ambassador/*` peuvent donc etre mises en cache par un proxy partage. La logique est aussi placee apres `UseStaticFiles`, donc elle n'apporte pas le cache attendu aux fichiers statiques deja servis.

**Impact** : fuite de reponses personnalisees entre utilisateurs et donnees perimees.  
**Correction** : `no-store, private` par defaut pour pages/API authentifiees et financieres; cache public uniquement sur ressources explicitement publiques/versionnees; ajouter `Vary` approprie.

### P1-3 - Confiance non bornee dans les en-tetes de proxy

`ForwardedHeadersOptions` active `X-Forwarded-For` et `X-Forwarded-Proto` sans definir `KnownProxies`/`KnownNetworks`, tandis que `AllowedHosts` vaut `*`.

**Impact** : usurpation d'IP/protocole selon la topologie, contournement possible des controles fondes sur IP et generation d'URL/host non fiable.  
**Correction** : declarer les proxys de confiance, limiter `ForwardLimit`, definir les hosts autorises et tester la terminaison TLS.

### P1-4 - Etat en memoire incompatible avec plusieurs instances

- Sessions : `AddDistributedMemoryCache` (`Program.cs:227`) n'est pas distribue.
- Rate limiting : implementation en memoire (`:287`).
- Presence SignalR et files d'activite : collections statiques/locales au processus.
- Plusieurs hosted services tournent dans chaque replica sans verrou distribue.

**Impact** : sessions perdues au redemarrage, limite contournable entre replicas, presence incoherente, commissions/reconciliations executees plusieurs fois.  
**Correction** : Redis/Postgres pour sessions/cache/rate limit, backplane SignalR, et jobs uniques via file durable/verrou distribue avec idempotence en base.

### P1-5 - Migrations automatiques au demarrage

`Program.cs:435-439` execute `Database.Migrate()` lors du demarrage de chaque instance.

**Impact** : course entre replicas, indisponibilite, verrous longs et deploiement irreversible sans validation.  
**Correction** : migrations dans une etape CI/CD unique, sauvegarde verifiee, revue SQL, strategie expand/contract et procedure de rollback.

### P1-6 - Cles Data Protection non persistantes en deploiement

Les cles sont placees dans `/app/keys`, mais `docker-compose.yml` ne monte aucun volume correspondant.

**Impact** : cookies d'authentification, antiforgery et jetons invalides apres recreation du conteneur; deconnexion generale et erreurs de formulaires.  
**Correction** : volume ou coffre partage protege, permissions minimales, sauvegarde et partage entre replicas.

### P1-7 - Logs contenant secrets et donnees financieres

- `MonCashService.cs:372` affiche explicitement le token d'acces.
- Plusieurs methodes affichent les reponses brutes de la passerelle (`:117`, `:164`, `:264`, `:339`, `:405`).
- La page de paiement affiche l'URL de paiement (`PaymentMethods.cshtml.cs:113`).
- De nombreux logs contiennent emails, IDs, montants et soldes.

**Impact** : compromission de credentials, fuite de PII/PCI-like data et fraude.  
**Correction** : supprimer secrets et corps bruts, journalisation structuree avec redaction, politique d'acces/retention et correlation IDs.

### P1-8 - Retraits/reservations et concurrence a renforcer

- `ReserveBalanceAsync` utilise une transaction par defaut et fait une lecture puis insertion sans verrouillage serialisable (`SecureFinancialService.cs:705-727`). Deux requetes concurrentes peuvent reserver le meme solde.
- `ConfirmBalanceReservationAsync` identifie une reservation seulement par utilisateur + montant (`:738-751`), ambigu si plusieurs reservations identiques existent.
- Le commentaire "lock pessimiste" ne correspond pas a un `FOR UPDATE`; la securite depend de l'isolation et des contraintes.

**Correction** : identifiant de reservation unique, transaction serialisable ou update atomique conditionnelle, contraintes de non-negativite, jeton d'idempotence et tests de concurrence.

### P1-9 - Absence de tests automatiques

Aucun projet de tests n'a ete trouve. Cela laisse sans filet les paiements, transitions de statut, stocks, commissions, remboursements, permissions et migrations.

**Correction minimale** : tests unitaires des calculs, tests d'integration PostgreSQL, tests d'autorisation multi-tenant, tests de concurrence/idempotence et tests E2E des parcours critiques.

## P2 - Risques importants

### P2-1 - Avertissements de compilation ignores

193 avertissements, notamment dereferencements potentiellement nuls, code inaccessible (`PlatformTransactionAuditService.cs`), methodes `async` sans `await`, doublons de `using` et annotations nullable incoherentes. `Nullable` est desactive globalement alors qu'une partie du code tente de l'utiliser.

**Action** : activer progressivement `Nullable`, traiter d'abord CS8600/CS8601/CS8602, supprimer le code inaccessible, puis etablir un budget de warnings a zero pour le code critique.

### P2-2 - Dependances vulnerables et versions incoherentes

Audit NuGet :

- `Microsoft.Build 17.8.3` transitive - severite haute - GHSA-w3q9-fxm7-j8fq.
- `NuGet.Packaging 6.11.0` transitive - faible.
- `NuGet.Protocol 6.11.0` transitive - faible.

Le projet cible .NET 9 tout en utilisant principalement des packages ASP.NET/EF Core 8 et une dependance SQL Server preview inutilisee dans un projet PostgreSQL.

**Action** : choisir une ligne de support coherente, mettre a jour les outils/packages responsables, retirer les providers inutilises et ajouter l'audit NuGet en CI.

### P2-3 - Configuration non validee au demarrage

Il n'existe pas de `ValidateOnStart`. Plusieurs services peuvent etre construits avec des valeurs absentes; Twilio est instancie directement avec des chaines potentiellement vides.

**Action** : options typees, annotations de validation, `ValidateOnStart`, controle de l'URL MonCash production et refus de demarrer si une dependance critique manque.

### P2-4 - Observabilite et reprise insuffisantes

Aucun health check, readiness/liveness, OpenTelemetry/APM, metriques Prometheus, sink de logs structure ou alerte n'a ete trouve. Le backup automatique est commente. Compose n'a pas de healthcheck.

**Action** : health checks application/base/passerelles, metriques de paiements/jobs, traces, alertes, sauvegardes chiffrees automatisees et restauration testee.

### P2-5 - Taches de fond non orchestrees

Sept hosted services sont demarres dans le processus web. Certains utilisent `Timer`, certains bouclent, et aucun mecanisme de leader election n'a ete trouve.

**Impact** : doublons en multi-instance, charge DB, perte de travail au redemarrage.  
**Action** : file durable ou scheduler avec verrou distribue, tables d'outbox/inbox et operations idempotentes.

### P2-6 - Overposting repandu

Plusieurs pages lient directement des entites EF completes puis les marquent `Modified`. Un client peut soumettre des champs non prevus par l'interface.

**Action** : ViewModels/DTOs dedies et mapping explicite; ne jamais attacher directement une entite recue du navigateur.

### P2-7 - Robustesse HTTP externe

Les clients MonCash/Stripe ne montrent pas de politiques uniformes de timeout, retry avec jitter, circuit breaker et idempotency key. Certaines exceptions sont avalees ou re-emballees en perdant le type et la pile utile.

**Action** : clients nommes, timeouts bornes, retries seulement sur operations sures, idempotency keys fournisseur, gestion typee des erreurs et correlation.

### P2-8 - Headers de securite absents

Aucune CSP, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy` ou protection frame explicite n'a ete trouvee.

**Action** : ajouter une politique testee, de preference CSP par nonce, et eliminer les scripts inline progressivement.

## P3 - Dette et maintenabilite

- `AddRazorPages` et `MapControllers` sont appeles deux fois dans `Program.cs`.
- Beaucoup de `Console.WriteLine`, commentaires temporaires et messages avec encodage corrompu.
- Plusieurs pages font des acces DB synchrones au milieu d'un flux async.
- Certaines vues/services sont volumineux et cumulent orchestration, validation, finance et notification.
- `.gitignore` ignore `.git/` et le dossier `.git` present est vide : la tracabilite locale n'est pas exploitable.
- Pas de documentation de runbook, SLO, rollback, rotation de secrets ou reponse aux incidents trouvee.

## Marche a suivre recommandee

### Phase 0 - Gel du lancement (immediat)

1. Ne pas activer les cles Stripe/MonCash de production.
2. Retirer les logs de jetons et de reponses brutes; considerer la rotation des secrets deja utilises.
3. Sauvegarder la base actuelle et retablir un depot Git valide.
4. Creer un environnement de staging isole avec des comptes et passerelles sandbox.

**Critere de sortie** : aucun secret/token dans les logs, historique Git disponible, staging reproductible.

### Phase 1 - Fermer les vulnerabilites P0

1. Ajouter une couche d'autorisation par ressource/tenant et corriger tous les handlers Seller.
2. Securiser SignalR et les handlers de conversation.
3. Refaire le flux MonCash autour d'une seule operation idempotente de settlement.
4. Proteger `/Payments` et valider toutes les entrees cote serveur.
5. Deplacer les preuves bancaires vers un stockage prive.
6. Corriger et tester l'image Docker .NET 9 / `MCBusiness.dll`.

**Critere de sortie** : tests prouvent qu'un vendeur/utilisateur ne peut jamais acceder aux ressources d'un autre; double webhook/retry ne credite qu'une fois; image Docker demarre et passe le smoke test.

### Phase 2 - Garantir l'integrite financiere

1. Formaliser une machine a etats pour commandes, paiements, expeditions, retraits, remboursements et commissions.
2. Ajouter contraintes DB : montants positifs, soldes non negatifs si applicable, references uniques, transitions idempotentes.
3. Utiliser un ledger append-only comme source de verite et rapprocher les soldes derives.
4. Ajouter outbox/inbox pour notifications et webhooks.
5. Tester concurrence, crash entre ecritures, rejeu, timeout fournisseur et remboursement.

**Critere de sortie** : bilan comptable reconciliable a zero ecart sur un jeu de tests de reference et apres injections de panne.

### Phase 3 - Industrialiser la qualite

1. Creer projets de tests unitaires, integration et E2E.
2. CI obligatoire : restore verrouille, build Release, tests, audit NuGet, analyse statique, build/scan image.
3. Reduire les 193 warnings, en commencant par les risques null/concurrence.
4. Ajouter migrations testees depuis une copie anonymisee de la base.

**Critere de sortie** : pipeline vert reproductible, couverture des parcours critiques et aucun warning critique.

### Phase 4 - Rendre l'exploitation fiable

1. Redis/cache partage, Data Protection persistante, backplane SignalR.
2. Jobs hors processus ou leader unique avec verrous distribues.
3. Health/readiness/liveness, logs structures rediges, traces et metriques.
4. Alertes : echec paiement, ecart de rapprochement, job bloque, taux d'erreur, saturation DB.
5. Sauvegardes chiffrees quotidiennes et exercice de restauration.

**Critere de sortie** : tableau de bord operationnel, alertes testees, RPO/RTO definis et restauration reussie.

### Phase 5 - Preproduction puis lancement controle

1. Tests de charge et de resilience.
2. Pentest cible sur IDOR, auth, uploads, webhooks et logique metier.
3. Revue juridique/confidentialite pour preuves bancaires et donnees personnelles.
4. Deploiement canari, limites de montants initiales, rapprochement manuel quotidien.
5. Plan rollback et astreinte pendant la fenetre de lancement.

**Critere GO** : aucun P0/P1 ouvert, pentest sans critique/haute non acceptee, restauration prouvee, reconciliation financiere validee et observabilite operationnelle.

## Ordre de correction conseille

1. Messagerie et isolation vendeurs.
2. Logs de secrets et stockage des preuves bancaires.
3. Paiements MonCash et idempotence financiere.
4. Docker/deploiement et migrations.
5. Tests automatises critiques.
6. Etat distribue, jobs et observabilite.
7. Dette de compilation et durcissement general.

## Estimation prudente

Avec une personne connaissant deja le projet : environ **4 a 8 semaines** pour atteindre un niveau de preproduction credible, puis **1 a 2 semaines** de validation, charge, pentest et corrections. Cette estimation depend fortement de l'etat reel de la base, des contrats MonCash/Stripe et du nombre de parcours metier a conserver.
