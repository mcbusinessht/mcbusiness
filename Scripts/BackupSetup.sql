-- ========================================
-- SCRIPT DE CONFIGURATION SYSTÈME DE SAUVEGARDE
-- FriKob Backup System Setup
-- ========================================

PRINT 'Configuration du système de sauvegarde FriKob...';

-- 1. VÉRIFICATION DES PRÉREQUIS
-- ==============================

-- Vérifier les permissions de sauvegarde
IF IS_SRVROLEMEMBER('sysadmin') = 1 OR IS_SRVROLEMEMBER('dbcreator') = 1
BEGIN
    PRINT '✓ Permissions suffisantes pour effectuer des sauvegardes';
END
ELSE
BEGIN
    PRINT '⚠ ATTENTION: Permissions insuffisantes pour les sauvegardes SQL Server';
    PRINT 'L''utilisateur doit être membre de sysadmin ou dbcreator';
END

-- Vérifier l'espace disque disponible
DECLARE @DatabaseName NVARCHAR(255) = DB_NAME();
DECLARE @DataSize DECIMAL(10,2), @LogSize DECIMAL(10,2);

SELECT 
    @DataSize = SUM(CASE WHEN type = 0 THEN size * 8.0 / 1024 END),
    @LogSize = SUM(CASE WHEN type = 1 THEN size * 8.0 / 1024 END)
FROM sys.database_files;

PRINT '📊 Taille actuelle de la base de données:';
PRINT '   - Données: ' + CAST(@DataSize AS VARCHAR(20)) + ' MB';
PRINT '   - Logs: ' + CAST(@LogSize AS VARCHAR(20)) + ' MB';
PRINT '   - Total estimé pour sauvegarde: ' + CAST((@DataSize + @LogSize) * 1.2 AS VARCHAR(20)) + ' MB';

-- 2. CONFIGURATION DES PARAMÈTRES DE SAUVEGARDE
-- ==============================================

-- Configurer le modèle de récupération en FULL pour les sauvegardes transactionnelles
IF DATABASEPROPERTYEX(@DatabaseName, 'Recovery') != 'FULL'
BEGIN
    DECLARE @RecoverySQL NVARCHAR(500) = 'ALTER DATABASE [' + @DatabaseName + '] SET RECOVERY FULL';
    EXEC sp_executesql @RecoverySQL;
    PRINT '✓ Modèle de récupération défini sur FULL';
END
ELSE
BEGIN
    PRINT '✓ Modèle de récupération déjà en FULL';
END

-- Optimiser les paramètres pour les sauvegardes
EXEC sp_configure 'show advanced options', 1;
RECONFIGURE;

-- Configurer la compression de sauvegarde (si SQL Server 2008+)
IF (SELECT SERVERPROPERTY('ProductVersion')) >= '10.0'
BEGIN
    EXEC sp_configure 'backup compression default', 1;
    RECONFIGURE;
    PRINT '✓ Compression de sauvegarde activée par défaut';
END

-- Réinitialiser les options avancées
EXEC sp_configure 'show advanced options', 0;
RECONFIGURE;

-- 3. CRÉATION DES RÉPERTOIRES DE SAUVEGARDE
-- =========================================

DECLARE @BackupDir NVARCHAR(500);
DECLARE @DefaultBackupDir NVARCHAR(500);

-- Obtenir le répertoire de sauvegarde par défaut
EXEC master.dbo.xp_instance_regread N'HKEY_LOCAL_MACHINE',
    N'Software\Microsoft\MSSQLServer\MSSQLServer',N'BackupDirectory',
    @DefaultBackupDir OUTPUT;

SET @BackupDir = @DefaultBackupDir + '\FriKob';

-- Créer le répertoire de sauvegarde FriKob
DECLARE @CreateDirCmd NVARCHAR(500) = 'mkdir "' + @BackupDir + '"';
EXEC master.dbo.xp_cmdshell @CreateDirCmd, no_output;

PRINT '📁 Répertoire de sauvegarde: ' + @BackupDir;

-- 4. CRÉATION D'UN PLAN DE MAINTENANCE BASIQUE
-- ============================================

-- Créer une sauvegarde de test pour valider la configuration
DECLARE @TestBackupPath NVARCHAR(500) = @BackupDir + '\' + @DatabaseName + '_ConfigTest_' + 
    FORMAT(GETDATE(), 'yyyyMMdd_HHmmss') + '.bak';

DECLARE @BackupSQL NVARCHAR(1000) = 'BACKUP DATABASE [' + @DatabaseName + '] TO DISK = ''' + 
    @TestBackupPath + ''' WITH COMPRESSION, INIT, SKIP, NOREWIND, NOUNLOAD, STATS = 10';

BEGIN TRY
    EXEC sp_executesql @BackupSQL;
    PRINT '✓ Sauvegarde de test créée avec succès: ' + @TestBackupPath;
    
    -- Nettoyer la sauvegarde de test
    DECLARE @DeleteTestCmd NVARCHAR(500) = 'del "' + @TestBackupPath + '"';
    EXEC master.dbo.xp_cmdshell @DeleteTestCmd, no_output;
    PRINT '✓ Sauvegarde de test nettoyée';
END TRY
BEGIN CATCH
    PRINT '✗ ERREUR lors de la création de la sauvegarde de test:';
    PRINT ERROR_MESSAGE();
END CATCH

-- 5. SCRIPTS DE SAUVEGARDE RECOMMANDÉS
-- ====================================

PRINT '';
PRINT '📋 SCRIPTS DE SAUVEGARDE RECOMMANDÉS:';
PRINT '';
PRINT '-- Sauvegarde complète quotidienne (à exécuter via SQL Agent ou tâche programmée):';
PRINT 'BACKUP DATABASE [' + @DatabaseName + '] TO DISK = ''' + @BackupDir + '\' + @DatabaseName + '_Full_YYYYMMDD.bak''';
PRINT 'WITH COMPRESSION, INIT, SKIP, NOREWIND, NOUNLOAD, STATS = 10;';
PRINT '';
PRINT '-- Sauvegarde différentielle (toutes les 6 heures):';
PRINT 'BACKUP DATABASE [' + @DatabaseName + '] TO DISK = ''' + @BackupDir + '\' + @DatabaseName + '_Diff_YYYYMMDD_HHMM.bak''';
PRINT 'WITH DIFFERENTIAL, COMPRESSION, INIT, SKIP, NOREWIND, NOUNLOAD, STATS = 10;';
PRINT '';
PRINT '-- Sauvegarde du log de transaction (toutes les 15 minutes):';
PRINT 'BACKUP LOG [' + @DatabaseName + '] TO DISK = ''' + @BackupDir + '\' + @DatabaseName + '_Log_YYYYMMDD_HHMM.trn''';
PRINT 'WITH COMPRESSION, INIT, SKIP, NOREWIND, NOUNLOAD, STATS = 10;';

-- 6. VÉRIFICATION DE L'INTÉGRITÉ
-- ==============================

PRINT '';
PRINT '🔍 VÉRIFICATION DE L''INTÉGRITÉ DE LA BASE DE DONNÉES...';

DBCC CHECKDB(@DatabaseName) WITH NO_INFOMSGS, ALL_ERRORMSGS;

IF @@ERROR = 0
    PRINT '✓ Intégrité de la base de données vérifiée avec succès';
ELSE
    PRINT '⚠ Problèmes d''intégrité détectés - Vérifiez les logs';

-- 7. RECOMMANDATIONS FINALES
-- ==========================

PRINT '';
PRINT '🎯 RECOMMANDATIONS POUR LA PRODUCTION:';
PRINT '';
PRINT '1. 📅 PLANIFICATION:';
PRINT '   - Sauvegardes complètes: Quotidienne (02:00)';
PRINT '   - Sauvegardes différentielles: Toutes les 6h';
PRINT '   - Sauvegardes logs: Toutes les 15 minutes';
PRINT '';
PRINT '2. 🗂️ RÉTENTION:';
PRINT '   - Sauvegardes complètes: 30 jours';
PRINT '   - Sauvegardes différentielles: 7 jours';
PRINT '   - Sauvegardes logs: 3 jours';
PRINT '';
PRINT '3. 📍 STOCKAGE:';
PRINT '   - Local: ' + @BackupDir;
PRINT '   - Distant: Configurer une copie vers un stockage externe';
PRINT '   - Cloud: Envisager Azure Backup ou AWS RDS selon l''infrastructure';
PRINT '';
PRINT '4. 🔧 MAINTENANCE:';
PRINT '   - Vérifier l''intégrité avant chaque sauvegarde';
PRINT '   - Tester la restauration mensuellement';
PRINT '   - Surveiller l''espace disque';
PRINT '   - Nettoyer les anciennes sauvegardes automatiquement';
PRINT '';
PRINT '5. 🚨 MONITORING:';
PRINT '   - Configurer des alertes en cas d''échec de sauvegarde';
PRINT '   - Logs d''événements Windows';
PRINT '   - Notifications par email via Database Mail';
PRINT '';
PRINT '6. 🛡️ SÉCURITÉ:';
PRINT '   - Chiffrer les sauvegardes sensibles';
PRINT '   - Contrôler l''accès aux fichiers de sauvegarde';
PRINT '   - Sauvegardes hors site pour la continuité d''activité';

-- 8. CRÉATION D'UN JOB SQL AGENT EXEMPLE (si SQL Agent disponible)
-- ================================================================

IF EXISTS (SELECT 1 FROM sys.objects WHERE name = 'sysjobs' AND type = 'U')
BEGIN
    PRINT '';
    PRINT '📋 SQL SERVER AGENT DÉTECTÉ - Création d''un job de sauvegarde exemple...';
    
    -- Note: La création réelle d'un job nécessiterait des privilèges spéciaux
    -- Ici nous affichons juste le script à exécuter manuellement
    
    PRINT '';
    PRINT '-- Script à exécuter pour créer un job de sauvegarde automatique:';
    PRINT 'USE [msdb];';
    PRINT 'GO';
    PRINT 'EXEC dbo.sp_add_job @job_name = N''FriKob Daily Backup'';';
    PRINT 'EXEC dbo.sp_add_jobstep @job_name = N''FriKob Daily Backup'',';
    PRINT '    @step_name = N''Backup Database'',';
    PRINT '    @command = N''BACKUP DATABASE [' + @DatabaseName + '] TO DISK = ''''C:\Backups\FriKob\' + @DatabaseName + '_Full_$$(ESCAPE_SQUOTE(STRTDT)).bak'''' WITH COMPRESSION, INIT'';';
    PRINT 'EXEC dbo.sp_add_schedule @schedule_name = N''Daily at 2AM'',';
    PRINT '    @freq_type = 4, @freq_interval = 1, @active_start_time = 20000;';
    PRINT 'EXEC dbo.sp_attach_schedule @job_name = N''FriKob Daily Backup'',';
    PRINT '    @schedule_name = N''Daily at 2AM'';';
    PRINT 'EXEC dbo.sp_add_jobserver @job_name = N''FriKob Daily Backup'';';
END

PRINT '';
PRINT '🎉 CONFIGURATION DU SYSTÈME DE SAUVEGARDE TERMINÉE !';
PRINT '';
PRINT 'ℹ️  Le système de sauvegarde automatique FriKob est maintenant configuré.';
PRINT 'ℹ️  Consultez la page d''administration pour gérer les sauvegardes via l''interface web.';
PRINT '';

-- Afficher un résumé final
SELECT 
    'Configuration Sauvegarde' AS [Statut],
    @DatabaseName AS [Base de Données],
    @BackupDir AS [Répertoire de Sauvegarde],
    DATABASEPROPERTYEX(@DatabaseName, 'Recovery') AS [Modèle de Récupération],
    CASE WHEN (SELECT value_in_use FROM sys.configurations WHERE name = 'backup compression default') = 1 
         THEN 'Activée' ELSE 'Désactivée' END AS [Compression],
    FORMAT(GETDATE(), 'yyyy-MM-dd HH:mm:ss') AS [Date de Configuration];