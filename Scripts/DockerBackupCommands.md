# 🐳 COMMANDES SAUVEGARDE DOCKER FRIKOB

## 📋 **COMMANDES MANUELLES POUR URGENCE**

### **🗄️ Sauvegarde Base de Données depuis Container**

```bash
# 1. Sauvegarder la base depuis le container Docker
docker exec sqlserver-container /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P 'YourPassword' \
  -Q "BACKUP DATABASE FriKobDb TO DISK = '/tmp/frikob_backup.bak' WITH FORMAT, INIT"

# 2. Copier le backup du container vers l'hôte
docker cp sqlserver-container:/tmp/frikob_backup.bak ./Backups/frikob_manual_backup.bak

# 3. Nettoyer le fichier temporaire dans le container
docker exec sqlserver-container rm /tmp/frikob_backup.bak
```

### **🔧 Restauration Base de Données**

```bash
# 1. Copier le backup vers le container
docker cp ./Backups/frikob_backup.bak sqlserver-container:/tmp/restore_backup.bak

# 2. Restaurer la base de données
docker exec sqlserver-container /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P 'YourPassword' \
  -Q "RESTORE DATABASE FriKobDb FROM DISK = '/tmp/restore_backup.bak' WITH REPLACE"

# 3. Nettoyer
docker exec sqlserver-container rm /tmp/restore_backup.bak
```

### **📦 Sauvegarde Container Complete**

```bash
# Créer une image du container actuel
docker commit sqlserver-container frikob-db-backup:$(date +%Y%m%d)

# Sauvegarder l'image
docker save frikob-db-backup:$(date +%Y%m%d) > ./Backups/frikob-container-backup.tar

# Compresser
gzip ./Backups/frikob-container-backup.tar
```

### **🔍 Vérification État Docker**

```bash
# Vérifier les containers en cours
docker ps

# Voir les logs du container SQL
docker logs sqlserver-container --tail 50

# Vérifier l'espace disque dans le container
docker exec sqlserver-container df -h

# Tester la connexion SQL
docker exec sqlserver-container /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P 'YourPassword' \
  -Q "SELECT @@VERSION"
```

## 🚨 **PROCÉDURES D'URGENCE**

### **Si le Container SQL ne répond plus :**

```bash
# 1. Redémarrer le container
docker restart sqlserver-container

# 2. Si échec, arrêter et relancer
docker stop sqlserver-container
docker start sqlserver-container

# 3. Vérifier les logs pour erreurs
docker logs sqlserver-container
```

### **Si corruption de base :**

```bash
# 1. Vérifier l'intégrité
docker exec sqlserver-container /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P 'YourPassword' \
  -Q "DBCC CHECKDB('FriKobDb')"

# 2. Réparation si nécessaire
docker exec sqlserver-container /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P 'YourPassword' \
  -Q "DBCC CHECKDB('FriKobDb', REPAIR_REBUILD)"
```

## ⚙️ **AUTOMATISATION DOCKER**

### **Script de sauvegarde automatique :**

```bash
#!/bin/bash
# docker-backup.sh

BACKUP_DIR="/host/path/to/Backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
CONTAINER_NAME="sqlserver-container"
DB_NAME="FriKobDb"

# Créer la sauvegarde
docker exec $CONTAINER_NAME /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P '$SA_PASSWORD' \
  -Q "BACKUP DATABASE $DB_NAME TO DISK = '/tmp/backup_$TIMESTAMP.bak' WITH FORMAT, INIT"

# Copier vers l'hôte
docker cp $CONTAINER_NAME:/tmp/backup_$TIMESTAMP.bak $BACKUP_DIR/

# Nettoyer
docker exec $CONTAINER_NAME rm /tmp/backup_$TIMESTAMP.bak

# Compresser
gzip $BACKUP_DIR/backup_$TIMESTAMP.bak

echo "Backup créé: $BACKUP_DIR/backup_$TIMESTAMP.bak.gz"
```

### **Crontab pour sauvegarde quotidienne :**

```bash
# Editer crontab
crontab -e

# Ajouter ligne pour sauvegarde à 2h du matin
0 2 * * * /path/to/docker-backup.sh >> /var/log/frikob-backup.log 2>&1
```