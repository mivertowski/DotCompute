#!/bin/bash
# Rollback Plan for Failed Fixes
# Provides safe rollback mechanism for problematic changes

set -e

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}=== DotCompute Rollback Plan ===${NC}"
echo "This script helps rollback problematic changes"
echo ""

# Check if we're in a git repository
if ! git rev-parse --git-dir > /dev/null 2>&1; then
    echo -e "${RED}Error: Not in a git repository${NC}"
    exit 1
fi

# Show current status
echo -e "${YELLOW}Current Git Status:${NC}"
git status --short
echo ""

# Create a backup branch if not exists
BACKUP_BRANCH="backup/hive-cleanup-$(date +%Y%m%d-%H%M%S)"
echo -e "${YELLOW}Creating backup branch: $BACKUP_BRANCH${NC}"
git branch "$BACKUP_BRANCH"
echo -e "${GREEN}✓ Backup branch created${NC}"
echo ""

# Show options
echo -e "${BLUE}Rollback Options:${NC}"
echo "1. Rollback specific file"
echo "2. Rollback all changes in directory"
echo "3. Rollback all uncommitted changes"
echo "4. Create stash and start fresh"
echo "5. Show diff of changes"
echo "6. Exit without changes"
echo ""

read -p "Select option (1-6): " option

case $option in
    1)
        echo ""
        echo -e "${YELLOW}Modified files:${NC}"
        git status --short | nl
        echo ""
        read -p "Enter file path to rollback: " filepath

        if [ -f "$filepath" ]; then
            echo -e "${YELLOW}Rolling back: $filepath${NC}"
            git checkout HEAD -- "$filepath"
            echo -e "${GREEN}✓ File rolled back successfully${NC}"
        else
            echo -e "${RED}File not found: $filepath${NC}"
        fi
        ;;

    2)
        read -p "Enter directory path to rollback: " dirpath

        if [ -d "$dirpath" ]; then
            echo -e "${YELLOW}Rolling back all changes in: $dirpath${NC}"
            read -p "Are you sure? (y/n): " confirm

            if [ "$confirm" = "y" ]; then
                git checkout HEAD -- "$dirpath"
                echo -e "${GREEN}✓ Directory rolled back successfully${NC}"
            else
                echo "Rollback cancelled"
            fi
        else
            echo -e "${RED}Directory not found: $dirpath${NC}"
        fi
        ;;

    3)
        echo -e "${YELLOW}This will rollback ALL uncommitted changes${NC}"
        read -p "Are you ABSOLUTELY sure? (yes/no): " confirm

        if [ "$confirm" = "yes" ]; then
            echo -e "${YELLOW}Rolling back all changes...${NC}"
            git reset --hard HEAD
            echo -e "${GREEN}✓ All changes rolled back${NC}"
        else
            echo "Rollback cancelled"
        fi
        ;;

    4)
        read -p "Enter stash message: " stash_msg
        echo -e "${YELLOW}Creating stash...${NC}"
        git stash push -m "$stash_msg"
        echo -e "${GREEN}✓ Changes stashed successfully${NC}"
        echo "To restore: git stash pop"
        ;;

    5)
        echo -e "${YELLOW}Showing diff of all changes:${NC}"
        git diff
        ;;

    6)
        echo "Exiting without changes"
        exit 0
        ;;

    *)
        echo -e "${RED}Invalid option${NC}"
        exit 1
        ;;
esac

echo ""
echo -e "${GREEN}Rollback operation completed${NC}"
echo -e "${BLUE}Backup branch available: $BACKUP_BRANCH${NC}"
