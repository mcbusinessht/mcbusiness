-- ============================================
-- SCRIPT : Créer un Ambassadeur de Test
-- ============================================

-- ÉTAPE 1 : Trouver ou créer un utilisateur
-- -------------------------------------------

-- Option A : Utilisez un utilisateur existant
SELECT "Id", "UserName", "Email", "ReferralCode", "IsAmbassador"
FROM "AspNetUsers"
WHERE "Email" = 'VOTRE_EMAIL@example.com'; -- Remplacez par votre email

-- Option B : Si vous voulez créer un nouveau utilisateur de test
-- (À faire via l'interface web : /Account/SignUp)


-- ÉTAPE 2 : Transformer l'utilisateur en ambassadeur
-- ---------------------------------------------------

-- Remplacez 'USER_ID_ICI' par l'ID de l'utilisateur de l'étape 1
UPDATE "AspNetUsers"
SET 
    "IsAmbassador" = true,
    "ReferralCode" = 'AMBASSADEUR123'  -- Ce sera votre code de référence
WHERE "Id" = 'USER_ID_ICI';

-- Vérification
SELECT "Id", "UserName", "Email", "ReferralCode", "IsAmbassador"
FROM "AspNetUsers"
WHERE "Id" = 'USER_ID_ICI';


-- ÉTAPE 3 : Ajouter le rôle Ambassador
-- --------------------------------------

-- Trouver l'ID du rôle Ambassador
SELECT "Id", "Name" FROM "AspNetRoles" WHERE "Name" = 'Ambassador';

-- Ajouter le rôle à l'utilisateur (remplacez les valeurs)
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
VALUES (
    'USER_ID_ICI',  -- ID de votre utilisateur
    (SELECT "Id" FROM "AspNetRoles" WHERE "Name" = 'Ambassador')
)
ON CONFLICT DO NOTHING;


-- ÉTAPE 4 : Créer un plan ambassadeur actif
-- -------------------------------------------

-- Vérifier s'il existe déjà un plan actif
SELECT "Id", "Name", "Price", "IsActive", "AmbassadorCommissionPercentage"
FROM "AmbassadorPlans"
WHERE "IsActive" = true;

-- Si aucun plan actif, en créer un
INSERT INTO "AmbassadorPlans" (
    "Name",
    "Description",
    "Price",
    "DurationInDays",
    "IsActive",
    "IsFreeplan",
    "AmbassadorCommissionPercentage",
    "WelcomeBonusPoints",
    "PointsGeneration1",
    "PointsGeneration2",
    "PointsGeneration3",
    "MonthlyPointsLimit",
    "Ceiling",
    "CreatedAt",
    "CurrencyBase"
)
VALUES (
    'Plan Test Ambassadeur',
    'Plan gratuit pour tester le système de commissions',
    0.00,
    30,  -- 30 jours
    true,  -- IsActive
    true,  -- IsFreeplan
    10.00,  -- 10% de commission
    100,  -- 100 points de bienvenue
    50,  -- Points génération 1
    30,  -- Points génération 2
    10,  -- Points génération 3
    1000,  -- Limite mensuelle
    10000.00,  -- Plafond
    NOW(),
    1  -- ID de la devise (HTG par exemple)
)
RETURNING "Id";


-- ÉTAPE 5 : Créer une souscription active
-- -----------------------------------------

-- Remplacez les valeurs
INSERT INTO "AmbassadorSubscriptions" (
    "UserId",
    "PlanId",
    "StartDate",
    "EndDate",
    "Status",
    "ReferralCount",
    "CreatedAt"
)
VALUES (
    'USER_ID_ICI',  -- ID de votre utilisateur
    (SELECT "Id" FROM "AmbassadorPlans" WHERE "Name" = 'Plan Test Ambassadeur' LIMIT 1),  -- ID du plan créé
    NOW(),
    NOW() + INTERVAL '30 days',
    'Active',
    0,
    NOW()
)
RETURNING "Id";


-- ÉTAPE 6 : VÉRIFICATION FINALE
-- ------------------------------

SELECT 
    u."Id" AS "UserId",
    u."UserName",
    u."Email",
    u."ReferralCode",
    u."IsAmbassador",
    CASE WHEN ur."RoleId" IS NOT NULL THEN 'Oui' ELSE 'Non' END AS "ARoleAmbassador",
    s."Id" AS "SubscriptionId",
    s."Status" AS "SubscriptionStatus",
    s."EndDate" AS "SubscriptionEndDate",
    p."Name" AS "PlanName",
    p."AmbassadorCommissionPercentage" AS "CommissionRate"
FROM "AspNetUsers" u
LEFT JOIN "AspNetUserRoles" ur ON u."Id" = ur."UserId" 
    AND ur."RoleId" = (SELECT "Id" FROM "AspNetRoles" WHERE "Name" = 'Ambassador')
LEFT JOIN "AmbassadorSubscriptions" s ON u."Id" = s."UserId" AND s."Status" = 'Active'
LEFT JOIN "AmbassadorPlans" p ON s."PlanId" = p."Id"
WHERE u."ReferralCode" = 'AMBASSADEUR123';

-- ✅ Vous devriez voir :
-- - IsAmbassador = true
-- - ARoleAmbassador = 'Oui'
-- - SubscriptionStatus = 'Active'
-- - CommissionRate = 10.00


-- ÉTAPE 7 : LIEN À TESTER
-- -------------------------

-- Votre lien ambassadeur sera :
-- http://localhost:5000/?ref=AMBASSADEUR123

-- Ou avec une entreprise spécifique :
-- http://localhost:5000/?ref=AMBASSADEUR123&company=1

-- Remplacez "1" par l'ID d'une vraie entreprise :
SELECT "Id", "Name", "Email" FROM "Companies" WHERE NOT "IsDeleted" LIMIT 5;
