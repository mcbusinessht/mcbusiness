-- ============================================================
-- SCRIPT DE TEST : Workflow Complet Commissions Ambassadeur
-- ============================================================
-- Ce script teste le flux complet des commissions ambassadeur
-- depuis la création d'une commande jusqu'au paiement final

-- ============================================================
-- ÉTAPE 1: VÉRIFIER LES DONNÉES DE BASE
-- ============================================================

-- 1.1 Vérifier qu'il existe des ambassadeurs avec abonnement actif
SELECT 
    u.Id,
    u.UserName,
    u.ReferralCode,
    u.IsAmbassador,
    s.Id AS SubscriptionId,
    s.Status AS SubscriptionStatus,
    s.EndDate,
    p.Name AS PlanName,
    p.AmbassadorCommissionPercentage
FROM "AspNetUsers" u
INNER JOIN "AmbassadorSubscriptions" s ON s.UserId = u.Id
INNER JOIN "AmbassadorPlans" p ON p.Id = s.PlanId
WHERE u.IsAmbassador = true 
  AND s.Status = 'Active'
  AND s.EndDate > NOW()
ORDER BY u.CreatedAt DESC
LIMIT 5;

-- 1.2 Vérifier les commandes livrées avec code ambassadeur
SELECT 
    o.Id,
    o.OrderDate,
    o.Status,
    o.TotalAmount,
    o.ReferredByAmbassador,
    u.UserName AS CustomerName,
    COUNT(ac.Id) AS CommissionsCreated
FROM "Orders" o
INNER JOIN "AspNetUsers" u ON u.Id = o.UserId
LEFT JOIN "AmbassadorCommissions" ac ON ac.OrderId = o.Id
WHERE o.ReferredByAmbassador IS NOT NULL
  AND o.Status = 'Delivered'
GROUP BY o.Id, o.OrderDate, o.Status, o.TotalAmount, o.ReferredByAmbassador, u.UserName
ORDER BY o.OrderDate DESC
LIMIT 10;

-- ============================================================
-- ÉTAPE 2: VÉRIFIER LES COMMISSIONS CRÉÉES
-- ============================================================

-- 2.1 Statistiques des commissions par statut
SELECT 
    Status,
    COUNT(*) AS Count,
    SUM(Amount) AS TotalAmount,
    AVG(Amount) AS AvgAmount
FROM "AmbassadorCommissions"
WHERE IsDeleted = false
GROUP BY Status
ORDER BY 
    CASE Status
        WHEN 'Pending' THEN 1
        WHEN 'Approved' THEN 2
        WHEN 'Available' THEN 3
        WHEN 'Paid' THEN 4
        WHEN 'Rejected' THEN 5
        WHEN 'Cancelled' THEN 6
        WHEN 'Refunded' THEN 7
    END;

-- 2.2 Détails des commissions récentes
SELECT 
    ac.Id,
    ac.CreatedAt,
    ac.Status,
    ac.Amount,
    ac.Type,
    ac.CommissionRate,
    ac.AvailableAt,
    ac.PaidAt,
    o.Id AS OrderId,
    o.TotalAmount AS OrderTotal,
    u.UserName AS AmbassadorName,
    u.ReferralCode
FROM "AmbassadorCommissions" ac
INNER JOIN "Orders" o ON o.Id = ac.OrderId
INNER JOIN "AspNetUsers" u ON u.Id = ac.AmbassadorId
WHERE ac.IsDeleted = false
ORDER BY ac.CreatedAt DESC
LIMIT 20;

-- ============================================================
-- ÉTAPE 3: VÉRIFIER L'AUDIT DES COMMISSIONS
-- ============================================================

-- 3.1 Comparer commissions payées vs audit PlatformTransaction
SELECT 
    'Commissions Paid' AS Source,
    COUNT(*) AS Count,
    SUM(Amount) AS TotalAmount
FROM "AmbassadorCommissions"
WHERE Status = 'Paid' OR Status = 'Available'

UNION ALL

SELECT 
    'Audited in PlatformTransaction' AS Source,
    COUNT(*) AS Count,
    ABS(SUM(Amount)) AS TotalAmount
FROM "PlatformTransactions"
WHERE Category = 'CommissionReferrer'
  AND IsDeleted = false;

-- 3.2 Identifier les commissions payées SANS audit (PROBLÈME)
SELECT 
    ac.Id AS CommissionId,
    ac.CreatedAt,
    ac.PaidAt,
    ac.Amount,
    ac.Status,
    o.Id AS OrderId,
    u.UserName AS AmbassadorName,
    CASE 
        WHEN pt.Id IS NULL THEN '❌ AUDIT MANQUANT'
        ELSE '✅ Audité'
    END AS AuditStatus
FROM "AmbassadorCommissions" ac
INNER JOIN "Orders" o ON o.Id = ac.OrderId
INNER JOIN "AspNetUsers" u ON u.Id = ac.AmbassadorId
LEFT JOIN "PlatformTransactions" pt ON pt.OrderId = ac.OrderId 
    AND pt.SupplierId = ac.AmbassadorId
    AND pt.Category = 'CommissionReferrer'
    AND pt.IsDeleted = false
WHERE ac.Status IN ('Paid', 'Available')
  AND ac.IsDeleted = false
ORDER BY ac.PaidAt DESC
LIMIT 50;

-- ============================================================
-- ÉTAPE 4: VÉRIFIER LES WALLETS AMBASSADEURS
-- ============================================================

-- 4.1 Vérifier les transactions wallet des ambassadeurs
SELECT 
    wt.Id,
    wt.CreatedAt,
    wt.Type,
    wt.Amount,
    wt.Description,
    u.UserName AS AmbassadorName,
    u.ReferralCode,
    wt.Balance AS WalletBalanceAfterTransaction
FROM "WalletTransactions" wt
INNER JOIN "AspNetUsers" u ON u.Id = wt.UserId
WHERE u.IsAmbassador = true
  AND wt.Type IN ('CommissionSale', 'CommissionReferral', 'Commission')
  AND wt.IsDeleted = false
ORDER BY wt.CreatedAt DESC
LIMIT 20;

-- 4.2 Balance totale par ambassadeur
SELECT 
    u.Id,
    u.UserName,
    u.ReferralCode,
    u.Balance AS UserBalance,
    (SELECT SUM(wt.Amount) 
     FROM "WalletTransactions" wt 
     WHERE wt.UserId = u.Id 
       AND wt.Type IN ('CommissionSale', 'CommissionReferral', 'Commission')
       AND wt.IsDeleted = false
    ) AS TotalCommissionsReceived,
    (SELECT COUNT(*) 
     FROM "AmbassadorCommissions" ac 
     WHERE ac.AmbassadorId = u.Id 
       AND ac.Status = 'Paid'
       AND ac.IsDeleted = false
    ) AS CommissionsPaidCount
FROM "AspNetUsers" u
WHERE u.IsAmbassador = true
ORDER BY TotalCommissionsReceived DESC NULLS LAST
LIMIT 10;

-- ============================================================
-- ÉTAPE 5: IDENTIFIER LES PROBLÈMES POTENTIELS
-- ============================================================

-- 5.1 Commissions bloquées en Approved depuis > 10 jours
SELECT 
    ac.Id,
    ac.CreatedAt,
    ac.AvailableAt,
    NOW() - ac.AvailableAt AS OverdueDuration,
    ac.Status,
    ac.Amount,
    o.Id AS OrderId,
    u.UserName AS AmbassadorName
FROM "AmbassadorCommissions" ac
INNER JOIN "Orders" o ON o.Id = ac.OrderId
INNER JOIN "AspNetUsers" u ON u.Id = ac.AmbassadorId
WHERE ac.Status = 'Approved'
  AND ac.AvailableAt IS NOT NULL
  AND ac.AvailableAt < NOW() - INTERVAL '10 days'
  AND ac.IsDeleted = false
ORDER BY ac.AvailableAt ASC;

-- 5.2 Commandes livrées SANS commissions créées (PROBLÈME)
SELECT 
    o.Id,
    o.OrderDate,
    o.Status,
    o.TotalAmount,
    o.ReferredByAmbassador,
    u.UserName AS CustomerName,
    amb.UserName AS AmbassadorName,
    CASE 
        WHEN ac.Id IS NULL THEN '❌ COMMISSIONS MANQUANTES'
        ELSE '✅ Commissions créées'
    END AS CommissionStatus
FROM "Orders" o
INNER JOIN "AspNetUsers" u ON u.Id = o.UserId
LEFT JOIN "AspNetUsers" amb ON amb.ReferralCode = o.ReferredByAmbassador
LEFT JOIN "AmbassadorCommissions" ac ON ac.OrderId = o.Id
WHERE o.Status = 'Delivered'
  AND o.ReferredByAmbassador IS NOT NULL
  AND o.ReferredByAmbassador != ''
  AND ac.Id IS NULL
ORDER BY o.OrderDate DESC
LIMIT 20;

-- 5.3 Vérifier la cohérence wallet plateforme
SELECT 
    w.Id AS WalletId,
    u.UserName,
    u.ReferralCode,
    w.Balance AS CurrentBalance,
    (SELECT SUM(Amount) FROM "PlatformTransactions" pt WHERE pt.WalletId = w.Id AND pt.IsDeleted = false) AS CalculatedBalance
FROM "Wallets" w
INNER JOIN "AspNetUsers" u ON u.Id = w.UserId
WHERE u.ReferralCode = '00000000'
LIMIT 1;

-- ============================================================
-- ÉTAPE 6: REQUÊTES D'ACTION
-- ============================================================

-- 6.1 Forcer la libération des commissions éligibles (ADMIN ONLY)
-- ATTENTION: À exécuter uniquement après vérification
/*
UPDATE "AmbassadorCommissions"
SET Status = 'Available'
WHERE Status = 'Approved'
  AND AvailableAt IS NOT NULL
  AND AvailableAt <= NOW()
  AND IsDeleted = false;
*/

-- 6.2 Statistiques globales
SELECT 
    'Total Orders' AS Metric,
    COUNT(*) AS Value
FROM "Orders"
WHERE ReferredByAmbassador IS NOT NULL

UNION ALL

SELECT 
    'Orders Delivered' AS Metric,
    COUNT(*) AS Value
FROM "Orders"
WHERE ReferredByAmbassador IS NOT NULL
  AND Status = 'Delivered'

UNION ALL

SELECT 
    'Commissions Created' AS Metric,
    COUNT(*) AS Value
FROM "AmbassadorCommissions"
WHERE IsDeleted = false

UNION ALL

SELECT 
    'Commissions Paid' AS Metric,
    COUNT(*) AS Value
FROM "AmbassadorCommissions"
WHERE Status IN ('Paid', 'Available')
  AND IsDeleted = false

UNION ALL

SELECT 
    'Total Amount Paid' AS Metric,
    SUM(Amount) AS Value
FROM "AmbassadorCommissions"
WHERE Status IN ('Paid', 'Available')
  AND IsDeleted = false;

-- ============================================================
-- RAPPORT FINAL
-- ============================================================
COMMENT ON TABLE "AmbassadorCommissions" IS 'Table des commissions ambassadeur - Vérifier régulièrement l''audit et les statuts bloqués';
