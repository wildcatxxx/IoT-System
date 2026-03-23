#!/bin/bash

# PostgreSQL Backup and Recovery Script
# For Disaster Recovery planning (Phase 3C)

set -e

BACKUP_DIR="backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/iot_db_$TIMESTAMP.sql"
DOCKER_CONTAINER="temperature-postgres"
DB_NAME="iot_db"
DB_USER="postgres"

# Create backups directory if it doesn't exist
mkdir -p "$BACKUP_DIR"

echo "================================"
echo "📊 PostgreSQL Database Backup"
echo "================================"

# Function: Create full database backup
backup_database() {
    echo ""
    echo "📋 Backing up database: $DB_NAME"
    
    # Check if container is running
    if ! docker ps | grep -q "$DOCKER_CONTAINER"; then
        echo "❌ Error: Docker container '$DOCKER_CONTAINER' is not running"
        return 1
    fi
    
    # Create backup
    docker exec -i "$DOCKER_CONTAINER" pg_dump -U "$DB_USER" "$DB_NAME" > "$BACKUP_FILE"
    
    if [ -f "$BACKUP_FILE" ]; then
        SIZE=$(du -h "$BACKUP_FILE" | cut -f1)
        echo "✅ Backup created successfully: $BACKUP_FILE ($SIZE)"
        return 0
    else
        echo "❌ Failed to create backup"
        return 1
    fi
}

# Function: Backup with compression (for storage)
backup_compressed() {
    echo ""
    echo "📋 Creating compressed backup..."
    
    local COMPRESSED_FILE="$BACKUP_DIR/iot_db_$TIMESTAMP.sql.gz"
    
    docker exec -i "$DOCKER_CONTAINER" pg_dump -U "$DB_USER" "$DB_NAME" | gzip > "$COMPRESSED_FILE"
    
    if [ -f "$COMPRESSED_FILE" ]; then
        SIZE=$(du -h "$COMPRESSED_FILE" | cut -f1)
        echo "✅ Compressed backup created: $COMPRESSED_FILE ($SIZE)"
        return 0
    else
        echo "❌ Failed to create compressed backup"
        return 1
    fi
}

# Function: Restore database from backup
restore_database() {
    local RESTORE_FILE="$1"
    
    if [ ! -f "$RESTORE_FILE" ]; then
        echo "❌ Error: Backup file not found: $RESTORE_FILE"
        return 1
    fi
    
    echo ""
    echo "⚠️  WARNING: This will overwrite the existing database!"
    read -p "Are you sure? Type 'yes' to continue: " CONFIRM
    
    if [ "$CONFIRM" != "yes" ]; then
        echo "Restore cancelled."
        return 1
    fi
    
    echo "📋 Restoring database from: $RESTORE_FILE"
    
    # Check if backup is compressed
    if [[ "$RESTORE_FILE" == *.gz ]]; then
        gunzip < "$RESTORE_FILE" | docker exec -i "$DOCKER_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME"
    else
        docker exec -i "$DOCKER_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" < "$RESTORE_FILE"
    fi
    
    if [ $? -eq 0 ]; then
        echo "✅ Database restored successfully"
        return 0
    else
        echo "❌ Failed to restore database"
        return 1
    fi
}

# Function: List backups
list_backups() {
    echo ""
    echo "📁 Available backups:"
    if [ -d "$BACKUP_DIR" ] && [ "$(ls -A $BACKUP_DIR)" ]; then
        ls -lh "$BACKUP_DIR" | awk '{print $9, "(" $5 ")"}'
    else
        echo "  No backups found"
    fi
}

# Function: Setup Point-in-Time Recovery (PITR)
setup_pitr() {
    echo ""
    echo "📋 Setting up Point-in-Time Recovery (PITR)..."
    echo ""
    echo "PITR requires:"
    echo "  1. WAL archiving enabled in PostgreSQL"
    echo "  2. Regular base backups"
    echo "  3. WAL files stored separately"
    echo ""
    
    # Create WAL archive directory
    mkdir -p "$BACKUP_DIR/wal_archive"
    
    cat > "$BACKUP_DIR/setup-pitr.sql" << 'EOF'
-- Run these commands as postgres superuser to enable PITR
ALTER SYSTEM SET wal_level = replica;
ALTER SYSTEM SET max_wal_senders = 3;
ALTER SYSTEM SET wal_keep_segments = 64;
SELECT pg_reload_conf();

-- Create archive command (adjust path as needed)
-- archive_command = 'test ! -f /backup/wal_archive/%f && cp %p /backup/wal_archive/%f'

-- Verify WAL archiving is enabled
SHOW wal_level;
SHOW max_wal_senders;
SHOW wal_keep_segments;
EOF

    echo "✅ PITR setup script created: $BACKUP_DIR/setup-pitr.sql"
    echo "   Run this script as PostgreSQL superuser to enable PITR"
}

# Function: Cleanup old backups (retention policy)
cleanup_old_backups() {
    local DAYS_TO_KEEP="${1:-30}"
    
    echo ""
    echo "🧹 Cleaning up backups older than $DAYS_TO_KEEP days..."
    
    find "$BACKUP_DIR" -name "*.sql*" -type f -mtime +$DAYS_TO_KEEP -delete
    
    echo "✅ Cleanup completed"
}

# Function: Automated daily backup (cron job)
setup_cron_backup() {
    echo ""
    echo "⏰ Setting up automated daily backup..."
    echo ""
    echo "Add this line to your crontab (crontab -e):"
    echo "  0 2 * * * /path/to/this/script.sh backup"
    echo ""
    echo "This will run backup daily at 2:00 AM"
}

# Main menu
case "${1:-menu}" in
    backup)
        backup_database && backup_compressed
        ;;
    backup-uncompressed)
        backup_database
        ;;
    backup-compressed)
        backup_compressed
        ;;
    restore)
        if [ -z "$2" ]; then
            echo "Error: Please specify backup file to restore"
            list_backups
            exit 1
        fi
        restore_database "$2"
        ;;
    list)
        list_backups
        ;;
    pitr)
        setup_pitr
        ;;
    cleanup)
        cleanup_old_backups "${2:-30}"
        ;;
    cron)
        setup_cron_backup
        ;;
    *)
        echo "Usage: $0 {backup|backup-uncompressed|backup-compressed|restore|list|pitr|cleanup|cron}"
        echo ""
        echo "Commands:"
        echo "  backup              - Create full backup (compressed + uncompressed)"
        echo "  backup-uncompressed - Create uncompressed SQL backup"
        echo "  backup-compressed   - Create compressed SQL.GZ backup"
        echo "  restore <file>      - Restore from backup file"
        echo "  list                - List all available backups"
        echo "  pitr                - Setup Point-in-Time Recovery"
        echo "  cleanup [days]      - Delete backups older than N days (default: 30)"
        echo "  cron                - Setup automated daily backup via cron"
        echo ""
        list_backups
        ;;
esac
