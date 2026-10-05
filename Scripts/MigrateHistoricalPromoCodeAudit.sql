-- ============================================================================
-- SCRIPT DE MIGRATION : Audit Historique des Codes Promo
-- ============================================================================
-- Objectif : Créer des entrées PlatformTransaction pour tous les PromoCodeUsages 
--            historiques qui n'ont pas encore été audités
-- Date : 2026-02-21
-- Auteur : MCBusiness Audit Team
-- ============================================================================

-- ÉTAPE 1 : Vérifier l'état actuel
-- ============================================================================
PRINT '📊 ÉTAT ACTUEL DES DONNÉES';
PRINT '================================================';

SELECT 
    COUNT(*) as TotalPromoCodeUsages,
    SUM(CAST("DiscountAmount" as DECIMAL(18,2))) as TotalDiscountAmount
FROM "PromoCodeUsages"
WHERE "DeletedAt" IS NULL;

SELECT 
    COUNT(*) as TotalPlatformTransactionsPromo
FROM "PlatformTransactions"
WHERE "Category" = 'PromoDiscount' AND "DeletedAt" IS NULL;

PRINT '';
PRINT '⚠️  DONNÉES À MIGRER';
PRINT '================================================';

-- Identifier les PromoCodeUsages sans audit
WITH MissingAudits AS (
    SELECT 
        pcu."Id" as PromoCodeUsageId,
        pcu."PromoCodeId",
        pcu."UsedByUserId",
        pcu."DiscountAmount",
        pcu."UsedAt",
        pcu."CreatedAt",
        pc."Code" as PromoCode,
        -- Tenter de trouver l'OrderId ou SubscriptionId associé
        p."OrderId",
        p."AmbassadorSubscriptionId"
    FROM "PromoCodeUsages" pcu
    INNER JOIN "PromoCodes" pc ON pc."Id" = pcu."PromoCodeId"
    LEFT JOIN "Payments" p ON p."PromoCodeUsageId" = pcu."Id"
    WHERE pcu."DeletedAt" IS NULL
      AND NOT EXISTS (
          SELECT 1 
          FROM "PlatformTransactions" pt
          WHERE pt."PromoCodeId" = pcu."PromoCodeId"
            AND pt."UserId" = pcu."UsedByUserId"
            AND pt."Amount" = pcu."DiscountAmount"
            AND pt."Category" = 'PromoDiscount'
            AND pt."DeletedAt" IS NULL
            AND ABS(EXTRACT(EPOCH FROM (pt."CreatedAt" - pcu."CreatedAt"))) < 60
      )
)
SELECT 
    COUNT(*) as PromoCodeUsagesSansAudit,
    SUM(CAST("DiscountAmount" as DECIMAL(18,2))) as MontantTotalNonAudite
FROM MissingAudits;

PRINT '';
PRINT '🔄 DÉBUT DE LA MIGRATION';
PRINT '================================================';

-- ÉTAPE 2 : Créer les entrées PlatformTransaction manquantes
-- ============================================================================
-- Note : Cette requête est conçue pour PostgreSQL
-- Pour SQL Server, remplacer RETURNING par OUTPUT INSERTED.*

INSERT INTO "PlatformTransactions" (
    "Amount",
    "Category",
    "Description",
    "UserId",
    "PromoCodeId",
    "OrderId",
    "SubscriptionId",
    "CreatedAt",
    "UpdatedAt"
)
SELECT 
    pcu."DiscountAmount" as Amount,
    'PromoDiscount' as Category,
    CONCAT('MIGRATION - Promotion ', 
           CASE 
               WHEN p."OrderId" IS NOT NULL THEN CONCAT('commande n°', p."OrderId")
               WHEN p."AmbassadorSubscriptionId" IS NOT NULL THEN CONCAT('abonnement n°', p."AmbassadorSubscriptionId")
               ELSE 'usage historique'
           END,
           ' - Code ', pc."Code") as Description,
    pcu."UsedByUserId" as UserId,
    pcu."PromoCodeId" as PromoCodeId,
    p."OrderId" as OrderId,
    p."AmbassadorSubscriptionId" as SubscriptionId,
    pcu."CreatedAt" as CreatedAt,
    CURRENT_TIMESTAMP as UpdatedAt
FROM "PromoCodeUsages" pcu
INNER JOIN "PromoCodes" pc ON pc."Id" = pcu."PromoCodeId"
LEFT JOIN "Payments" p ON p."PromoCodeUsageId" = pcu."Id"
WHERE pcu."DeletedAt" IS NULL
  AND NOT EXISTS (
      SELECT 1 
      FROM "PlatformTransactions" pt
      WHERE pt."PromoCodeId" = pcu."PromoCodeId"
        AND pt."UserId" = pcu."UsedByUserId"
        AND pt."Amount" = pcu."DiscountAmount"
        AND pt."Category" = 'PromoDiscount'
        AND pt."DeletedAt" IS NULL
        AND ABS(EXTRACT(EPOCH FROM (pt."CreatedAt" - pcu."CreatedAt"))) < 60
  );

-- Récupérer le nombre de lignes insérées
GET DIAGNOSTICS @rowcount = ROW_COUNT;
PRINT CONCAT('✅ ', @rowcount, ' entrées PlatformTransaction créées');

PRINT '';
PRINT '✅ VÉRIFICATION POST-MIGRATION';
PRINT '================================================';

-- ÉTAPE 3 : Vérifier que tous les PromoCodeUsages ont maintenant un audit
-- ============================================================================
SELECT 
    COUNT(*) as TotalPromoCodeUsages,
    COUNT(pt."Id") as PromoCodeUsagesAvecAudit,
    COUNT(*) - COUNT(pt."Id") as PromoCodeUsagesSansAudit
FROM "PromoCodeUsages" pcu
LEFT JOIN "PlatformTransactions" pt ON 
    pt."PromoCodeId" = pcu."PromoCodeId"
    AND pt."UserId" = pcu."UsedByUserId"
    AND pt."Amount" = pcu."DiscountAmount"
    AND pt."Category" = 'PromoDiscount'
    AND pt."DeletedAt" IS NULL
    AND ABS(EXTRACT(EPOCH FROM (pt."CreatedAt" - pcu."CreatedAt"))) < 60
WHERE pcu."DeletedAt" IS NULL;

PRINT '';
PRINT '📈 STATISTIQUES FINALES';
PRINT '================================================';

-- Statistiques par campagne
SELECT 
    pc."Code" as CodePromo,
    pcam."Name" as Campagne,
    COUNT(pcu."Id") as NombreUtilisations,
    SUM(CAST(pcu."DiscountAmount" as DECIMAL(18,2))) as MontantTotalRemises,
    COUNT(pt."Id") as UtilisationsAuditees
FROM "PromoCodes" pc
INNER JOIN "PromoCampaigns" pcam ON pcam."Id" = pc."PromoCampaignId"
LEFT JOIN "PromoCodeUsages" pcu ON pcu."PromoCodeId" = pc."Id" AND pcu."DeletedAt" IS NULL
LEFT JOIN "PlatformTransactions" pt ON 
    pt."PromoCodeId" = pc."Id"
    AND pt."Category" = 'PromoDiscount'
    AND pt."DeletedAt" IS NULL
WHERE pc."DeletedAt" IS NULL
GROUP BY pc."Code", pcam."Name"
ORDER BY MontantTotalRemises DESC;

PRINT '';
PRINT '🎉 MIGRATION TERMINÉE AVEC SUCCÈS';
PRINT '================================================';

-- ============================================================================
-- REQUÊTES DE VALIDATION RECOMMANDÉES
-- ============================================================================
-- Exécuter ces requêtes manuellement après la migration pour validation

-- 1. Vérifier l'intégrité des montants
/*
SELECT 
    SUM(CAST(pcu."DiscountAmount" as DECIMAL(18,2))) as TotalPromoCodeUsages,
    SUM(CAST(pt."Amount" as DECIMAL(18,2))) as TotalPlatformTransactions
FROM "PromoCodeUsages" pcu
INNER JOIN "PlatformTransactions" pt ON 
    pt."PromoCodeId" = pcu."PromoCodeId"
    AND pt."UserId" = pcu."UsedByUserId"
    AND pt."Category" = 'PromoDiscount';
*/

-- 2. Identifier les éventuelles incohérences
/*
SELECT 
    pcu."Id",
    pcu."DiscountAmount",
    pcu."UsedAt",
    'Pas d audit trouvé' as Statut
FROM "PromoCodeUsages" pcu
WHERE pcu."DeletedAt" IS NULL
  AND NOT EXISTS (
      SELECT 1 FROM "PlatformTransactions" pt
      WHERE pt."PromoCodeId" = pcu."PromoCodeId"
        AND pt."UserId" = pcu."UsedByUserId"
        AND pt."Category" = 'PromoDiscount'
        AND pt."DeletedAt" IS NULL
  );
*/
