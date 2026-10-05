-- ========================================
-- SCRIPT D'OPTIMISATION BASE DE DONNÉES
-- FriKob Performance Optimization
-- ========================================

-- 1. INDEXES POUR LES RECHERCHES FRÉQUENTES
-- ==========================================

-- Index pour ProductVariants (très utilisé)
CREATE NONCLUSTERED INDEX [IX_ProductVariants_IsAvailable_IsDeleted] 
ON [ProductVariants] ([IsAvailable], [IsDeleted])
INCLUDE ([ProductId], [Price], [CurrencyBase]);

-- Index pour Products (filtres fréquents)
CREATE NONCLUSTERED INDEX [IX_Products_IsDeleted] 
ON [Products] ([IsDeleted])
INCLUDE ([Name], [CategoryId]);

-- Index pour Categories (navigation)
CREATE NONCLUSTERED INDEX [IX_Categories_ParentCategoryId_IsActive] 
ON [Categories] ([ParentCategoryId], [IsActive])
INCLUDE ([Name], [Type]);

-- Index pour Users (recherche par ReferralCode)
CREATE NONCLUSTERED INDEX [IX_Users_ReferralCode] 
ON [AspNetUsers] ([ReferralCode])
INCLUDE ([Id], [UserName]);

-- Index pour Wallets (dashboard admin)
CREATE NONCLUSTERED INDEX [IX_Wallets_UserId] 
ON [Wallets] ([UserId])
INCLUDE ([Balance]);

-- Index pour AmbassadorSubscriptions (dashboard)
CREATE NONCLUSTERED INDEX [IX_AmbassadorSubscriptions_Status] 
ON [AmbassadorSubscriptions] ([Status])
INCLUDE ([UserId], [CreatedAt]);

-- Index pour Transactions (dashboard et rapports)
CREATE NONCLUSTERED INDEX [IX_Transactions_Status] 
ON [Transactions] ([Status])
INCLUDE ([Amount], [CreatedAt]);

-- Index pour Orders (recherche par utilisateur)
CREATE NONCLUSTERED INDEX [IX_Orders_UserId_Status] 
ON [Orders] ([UserId], [Status])
INCLUDE ([CreatedAt], [TotalAmount]);

-- Index pour ProductImages (chargement des images)
CREATE NONCLUSTERED INDEX [IX_ProductImages_ProductVariantId] 
ON [ProductImages] ([ProductVariantId])
INCLUDE ([ImagePath], [IsMain]);

-- 2. STATISTIQUES ET MAINTENANCE
-- ===============================

-- Mettre à jour les statistiques
UPDATE STATISTICS [ProductVariants];
UPDATE STATISTICS [Products];
UPDATE STATISTICS [Categories];
UPDATE STATISTICS [AspNetUsers];
UPDATE STATISTICS [Wallets];
UPDATE STATISTICS [Transactions];

-- 3. REQUÊTES D'ANALYSE PERFORMANCE
-- ==================================

-- Analyser les requêtes les plus lentes
SELECT TOP 10
    total_worker_time/execution_count AS avg_cpu_time,
    execution_count,
    total_elapsed_time/execution_count AS avg_elapsed_time,
    SUBSTRING(st.text, (qs.statement_start_offset/2)+1,
        ((CASE qs.statement_end_offset
            WHEN -1 THEN DATALENGTH(st.text)
            ELSE qs.statement_end_offset
        END - qs.statement_start_offset)/2) + 1) AS statement_text
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
ORDER BY total_worker_time/execution_count DESC;

-- Analyser l'utilisation des indexes
SELECT 
    OBJECT_NAME(s.object_id) AS table_name,
    i.name AS index_name,
    s.user_seeks,
    s.user_scans,
    s.user_lookups,
    s.user_updates
FROM sys.dm_db_index_usage_stats s
INNER JOIN sys.indexes i ON s.object_id = i.object_id AND s.index_id = i.index_id
WHERE s.database_id = DB_ID()
    AND OBJECT_NAME(s.object_id) IN ('Products', 'ProductVariants', 'Categories', 'AspNetUsers')
ORDER BY s.user_seeks + s.user_scans + s.user_lookups DESC;

-- 4. RECOMMANDATIONS DE MAINTENANCE
-- ==================================

-- Script à exécuter régulièrement (hebdomadaire)
-- EXEC sp_updatestats;
-- 
-- Script de réorganisation des indexes (mensuel)
-- ALTER INDEX ALL ON [Products] REORGANIZE;
-- ALTER INDEX ALL ON [ProductVariants] REORGANIZE;
-- ALTER INDEX ALL ON [Categories] REORGANIZE;
--
-- Script de reconstruction des indexes (trimestriel)
-- ALTER INDEX ALL ON [Products] REBUILD;
-- ALTER INDEX ALL ON [ProductVariants] REBUILD;
-- ALTER INDEX ALL ON [Categories] REBUILD;

-- 5. MONITORING CONTINU
-- ======================

-- Requête pour surveiller les pages lentes (à exécuter régulièrement)
SELECT TOP 20
    req.session_id,
    req.request_id,
    req.start_time,
    req.status,
    req.command,
    req.cpu_time,
    req.reads,
    req.writes,
    req.logical_reads,
    SUBSTRING(txt.text, (req.statement_start_offset/2)+1,
        ((CASE req.statement_end_offset
            WHEN -1 THEN DATALENGTH(txt.text)
            ELSE req.statement_end_offset
        END - req.statement_start_offset)/2) + 1) AS statement_text
FROM sys.dm_exec_requests req
CROSS APPLY sys.dm_exec_sql_text(req.sql_handle) txt
WHERE req.session_id > 50
ORDER BY req.cpu_time DESC;

PRINT 'Optimisation de la base de données terminée !';
PRINT 'Recommandations :';
PRINT '1. Exécuter sp_updatestats chaque semaine';
PRINT '2. Réorganiser les indexes chaque mois';
PRINT '3. Surveiller les requêtes lentes régulièrement';
PRINT '4. Considérer la compression de données pour les grandes tables';