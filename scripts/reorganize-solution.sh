#!/bin/bash
# DotCompute Solution Reorganization Script (Bash version)
# This script reorganizes the solution according to .NET best practices

set -euo pipefail

# Default values
DRY_RUN=false
FORCE=false
BACKUP_PATH="./backup"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --force)
            FORCE=true
            shift
            ;;
        --backup-path)
            BACKUP_PATH="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [--dry-run] [--force] [--backup-path PATH]"
            echo "  --dry-run       Show what would be done without making changes"
            echo "  --force         Continue even with uncommitted changes"
            echo "  --backup-path   Path for backup (default: ./backup)"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Color output functions
info() { echo -e "\e[34mℹ️  $1\e[0m"; }
success() { echo -e "\e[32m✅ $1\e[0m"; }
warning() { echo -e "\e[33m⚠️  $1\e[0m"; }
error() { echo -e "\e[31m❌ $1\e[0m"; }

echo -e "\e[36m🔄 DotCompute Solution Reorganization Script\e[0m"
echo -e "\e[36m=============================================\e[0m"

# Project mappings (source -> destination)
declare -A PROJECT_MAPPINGS=(
    # Core Projects
    ["src/DotCompute.Abstractions"]="src/Core/DotCompute.Abstractions"
    ["src/DotCompute.Core"]="src/Core/DotCompute.Core"
    ["src/DotCompute.Memory"]="src/Core/DotCompute.Memory"
    
    # Backend Projects
    ["plugins/backends/DotCompute.Backends.CPU"]="src/Backends/DotCompute.Backends.CPU"
    ["plugins/backends/DotCompute.Backends.CUDA"]="src/Backends/DotCompute.Backends.CUDA"
    ["plugins/backends/DotCompute.Backends.Metal"]="src/Backends/DotCompute.Backends.Metal"
    
    # Extension Projects
    ["src/DotCompute.Linq"]="src/Extensions/DotCompute.Linq"
    ["src/DotCompute.Algorithms"]="src/Extensions/DotCompute.Algorithms"
    
    # Runtime Projects
    ["src/DotCompute.Runtime"]="src/Runtime/DotCompute.Runtime"
    ["src/DotCompute.Plugins"]="src/Runtime/DotCompute.Plugins"
    ["src/DotCompute.Generators"]="src/Runtime/DotCompute.Generators"
    
    # Test Projects that need moving
    ["tests/DotCompute.Algorithms.Tests"]="tests/Unit/DotCompute.Algorithms.Tests"
    ["tests/DotCompute.Memory.Tests"]="tests/Unit/DotCompute.Memory.Tests"
    ["tests/DotCompute.Linq.Tests"]="tests/Unit/DotCompute.Linq.Tests"
    ["tests/DotCompute.Backends.CPU.Tests"]="tests/Unit/DotCompute.Backends.CPU.Tests"
    ["plugins/backends/DotCompute.Backends.CPU/tests"]="tests/Unit/DotCompute.Backends.CPU.Tests"
    ["plugins/backends/DotCompute.Backends.Metal/tests"]="tests/Unit/DotCompute.Backends.Metal.Tests"
)

check_prerequisites() {
    info "Checking prerequisites..."
    
    # Check if we're in the right directory
    if [[ ! -f "DotCompute.sln" ]]; then
        error "DotCompute.sln not found. Please run this script from the solution root."
        exit 1
    fi
    
    # Check git status
    if git status --porcelain 2>/dev/null | grep -q .; then
        if [[ "$FORCE" != "true" ]]; then
            error "Working directory has uncommitted changes. Use --force to continue anyway."
            exit 1
        else
            warning "Continuing with uncommitted changes (--force specified)"
        fi
    fi
    
    success "Prerequisites check passed"
}

create_backup() {
    if [[ "$DRY_RUN" != "true" ]]; then
        info "Creating backup..."
        local timestamp=$(date +"%Y%m%d_%H%M%S")
        local backup_dir="$BACKUP_PATH/dotcompute_backup_$timestamp"
        
        mkdir -p "$BACKUP_PATH"
        cp -r . "$backup_dir" 2>/dev/null || {
            # Create backup excluding large directories
            rsync -av --exclude='bin/' --exclude='obj/' --exclude='.git/' \
                  --exclude='TestResults/' --exclude='artifacts/' \
                  --exclude='coverage*/' . "$backup_dir/"
        }
        
        success "Backup created at: $backup_dir"
        echo "$backup_dir"
    else
        info "Dry run: Backup would be created"
        echo "dry-run-backup"
    fi
}

create_new_structure() {
    info "Creating new directory structure..."
    
    local new_dirs=(
        "src/Core"
        "src/Backends"
        "src/Extensions"
        "src/Runtime"
        "tests/Unit"
        "tests/Integration"
        "tests/Performance"
        "tests/Shared"
    )
    
    for dir in "${new_dirs[@]}"; do
        if [[ "$DRY_RUN" != "true" ]]; then
            if [[ ! -d "$dir" ]]; then
                mkdir -p "$dir"
                success "Created directory: $dir"
            fi
        else
            info "Dry run: Would create directory: $dir"
        fi
    done
}

move_projects() {
    info "Moving projects to new locations..."
    
    for source in "${!PROJECT_MAPPINGS[@]}"; do
        local destination="${PROJECT_MAPPINGS[$source]}"
        
        if [[ -d "$source" ]]; then
            info "Moving $source -> $destination"
            
            if [[ "$DRY_RUN" != "true" ]]; then
                # Create destination directory if it doesn't exist
                local dest_dir=$(dirname "$destination")
                mkdir -p "$dest_dir"
                
                # Move the project
                mv "$source" "$destination"
                success "Moved: $source -> $destination"
            else
                info "Dry run: Would move $source -> $destination"
            fi
        else
            warning "Source not found: $source"
        fi
    done
}

update_project_references() {
    info "Updating project references..."
    
    # Find all .csproj files
    find . -name "*.csproj" -type f | while IFS= read -r project_file; do
        info "Processing: $project_file"
        
        if [[ "$DRY_RUN" != "true" ]]; then
            # Create a temporary file for sed operations
            local temp_file=$(mktemp)
            cp "$project_file" "$temp_file"
            
            # Update various reference patterns
            sed -i 's|\.\.\\DotCompute\.Abstractions\\|..\\DotCompute.Abstractions\\|g' "$temp_file"
            sed -i 's|\.\.\\DotCompute\.Core\\|..\\DotCompute.Core\\|g' "$temp_file"
            sed -i 's|\.\.\\DotCompute\.Memory\\|..\\DotCompute.Memory\\|g' "$temp_file"
            
            # Backend to Core references
            sed -i 's|\.\.\\\.\.\\\.\.\\src\\DotCompute\.Abstractions\\|..\\..\\Core\\DotCompute.Abstractions\\|g' "$temp_file"
            sed -i 's|\.\.\\\.\.\\\.\.\\src\\DotCompute\.Core\\|..\\..\\Core\\DotCompute.Core\\|g' "$temp_file"
            sed -i 's|\.\.\\\.\.\\\.\.\\src\\DotCompute\.Memory\\|..\\..\\Core\\DotCompute.Memory\\|g' "$temp_file"
            sed -i 's|\.\.\\\.\.\\\.\.\\src\\DotCompute\.Plugins\\|..\\..\\Runtime\\DotCompute.Plugins\\|g' "$temp_file"
            
            # Extension to Core references
            sed -i 's|\.\.\\DotCompute\.Abstractions\\|..\\Core\\DotCompute.Abstractions\\|g' "$temp_file"
            sed -i 's|\.\.\\DotCompute\.Core\\|..\\Core\\DotCompute.Core\\|g' "$temp_file"
            sed -i 's|\.\.\\DotCompute\.Memory\\|..\\Core\\DotCompute.Memory\\|g' "$temp_file"
            
            # Test to source references
            sed -i 's|\.\.\\\.\.\\src\\DotCompute\.Abstractions\\|..\\..\\src\\Core\\DotCompute.Abstractions\\|g' "$temp_file"
            sed -i 's|\.\.\\\.\.\\src\\DotCompute\.Core\\|..\\..\\src\\Core\\DotCompute.Core\\|g' "$temp_file"
            sed -i 's|\.\.\\\.\.\\src\\DotCompute\.Memory\\|..\\..\\src\\Core\\DotCompute.Memory\\|g' "$temp_file"
            sed -i 's|\.\.\\\.\.\\src\\DotCompute\.Linq\\|..\\..\\src\\Extensions\\DotCompute.Linq\\|g' "$temp_file"
            sed -i 's|\.\.\\\.\.\\src\\DotCompute\.Algorithms\\|..\\..\\src\\Extensions\\DotCompute.Algorithms\\|g' "$temp_file"
            sed -i 's|\.\.\\\.\.\\src\\DotCompute\.Runtime\\|..\\..\\src\\Runtime\\DotCompute.Runtime\\|g' "$temp_file"
            sed -i 's|\.\.\\\.\.\\src\\DotCompute\.Plugins\\|..\\..\\src\\Runtime\\DotCompute.Plugins\\|g' "$temp_file"
            sed -i 's|\.\.\\\.\.\\src\\DotCompute\.Generators\\|..\\..\\src\\Runtime\\DotCompute.Generators\\|g' "$temp_file"
            
            # Backend references from tests
            sed -i 's|\.\.\\\.\.\\plugins\\backends\\DotCompute\.Backends\.CPU\\|..\\..\\src\\Backends\\DotCompute.Backends.CPU\\|g' "$temp_file"
            sed -i 's|\.\.\\\.\.\\plugins\\backends\\DotCompute\.Backends\.CUDA\\|..\\..\\src\\Backends\\DotCompute.Backends.CUDA\\|g' "$temp_file"
            sed -i 's|\.\.\\\.\.\\plugins\\backends\\DotCompute\.Backends\.Metal\\|..\\..\\src\\Backends\\DotCompute.Backends.Metal\\|g' "$temp_file"
            
            # Only update if there were changes
            if ! cmp -s "$project_file" "$temp_file"; then
                mv "$temp_file" "$project_file"
                success "Updated references in: $(basename "$project_file")"
            else
                rm "$temp_file"
            fi
        else
            info "Dry run: Would update references in: $(basename "$project_file")"
        fi
    done
}

update_solution_file() {
    info "Updating solution file..."
    
    if [[ "$DRY_RUN" != "true" ]]; then
        # Create backup of solution file
        cp "DotCompute.sln" "DotCompute.sln.bak"
        
        # Update project paths in solution
        for source in "${!PROJECT_MAPPINGS[@]}"; do
            local old_path="${source//\//\\}"
            local new_path="${PROJECT_MAPPINGS[$source]//\//\\}"
            sed -i "s|$old_path|$new_path|g" "DotCompute.sln"
        done
        
        success "Updated solution file"
    else
        info "Dry run: Would update solution file"
    fi
}

remove_empty_directories() {
    info "Cleaning up empty directories..."
    
    local empty_dirs=("plugins/backends" "plugins")
    
    for dir in "${empty_dirs[@]}"; do
        if [[ -d "$dir" ]] && [[ -z "$(find "$dir" -mindepth 1 -type f)" ]]; then
            if [[ "$DRY_RUN" != "true" ]]; then
                rmdir "$dir" 2>/dev/null || rm -rf "$dir"
                success "Removed empty directory: $dir"
            else
                info "Dry run: Would remove empty directory: $dir"
            fi
        fi
    done
}

test_build() {
    info "Testing build after reorganization..."
    
    if [[ "$DRY_RUN" != "true" ]]; then
        if dotnet build --no-restore --configuration Debug; then
            success "Build test passed"
        else
            error "Build test failed"
            return 1
        fi
    else
        info "Dry run: Would test build"
    fi
}

# Main execution
main() {
    if [[ "$DRY_RUN" == "true" ]]; then
        warning "DRY RUN MODE - No changes will be made"
    fi
    
    check_prerequisites
    
    local backup_path
    backup_path=$(create_backup)
    
    create_new_structure
    move_projects
    update_project_references
    update_solution_file
    remove_empty_directories
    
    if [[ "$DRY_RUN" != "true" ]]; then
        if ! test_build; then
            error "Build failed after reorganization"
            if [[ "$backup_path" != "dry-run-backup" ]] && [[ -d "$backup_path" ]]; then
                info "You can restore from backup at: $backup_path"
            fi
            exit 1
        fi
    fi
    
    success "✨ Solution reorganization completed successfully!"
    
    if [[ "$DRY_RUN" != "true" ]]; then
        info "Next steps:"
        info "1. Run 'dotnet build' to verify everything works"
        info "2. Run 'dotnet test' to verify all tests pass"
        info "3. Update any CI/CD scripts with new paths"
        info "4. Update documentation with new structure"
        info "5. Commit the changes to git"
        info ""
        info "Backup created at: $backup_path"
    else
        info "This was a dry run. Use the script without --dry-run to apply changes."
    fi
}

# Run main function
main "$@"