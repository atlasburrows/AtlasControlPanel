#!/bin/bash

##############################################################################
# OpenClaw Installation & Patcher Script (Linux/macOS)
#
# Complete setup script that:
# 1. Verifies Node.js 18+ is installed
# 2. Installs OpenClaw globally via npm if not present
# 3. Applies the splitToolExecuteArgs patch (PR #14982)
# 4. Returns the OpenClaw installation path
#
# Usage:
#   ./install-openclaw.sh
#   ./install-openclaw.sh --skip-patch
#   ./install-openclaw.sh --force
##############################################################################

set -o pipefail

# Colors
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly CYAN='\033[0;36m'
readonly NC='\033[0m' # No Color

# Logging functions
log_success() {
    echo -e "${GREEN}✓${NC} $1"
}

log_error() {
    echo -e "${RED}✗${NC} $1" >&2
}

log_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

log_info() {
    echo -e "${CYAN}ℹ${NC} $1"
}

# Check if Node.js is installed and meets version requirement
test_nodejs() {
    local version_output
    version_output=$(node --version 2>/dev/null || echo "")
    
    if [[ -z "$version_output" ]]; then
        echo "NOT_INSTALLED"
        return 1
    fi

    # Parse version (e.g., "v22.18.0" -> 22)
    local major_version
    major_version=$(echo "$version_output" | sed 's/v\([0-9]*\).*/\1/')
    
    if [[ -z "$major_version" ]] || ! [[ "$major_version" =~ ^[0-9]+$ ]]; then
        echo "INVALID"
        return 1
    fi

    if [[ $major_version -lt 18 ]]; then
        echo "$version_output:INSUFFICIENT"
        return 1
    fi

    echo "$version_output:OK"
    return 0
}

# Get npm global prefix
get_npm_global_prefix() {
    npm config get prefix 2>/dev/null || echo "${HOME}/.npm-global"
}

# Check if OpenClaw is already installed
test_openclaw_installed() {
    local npm_prefix="$1"
    local search_paths=(
        "${npm_prefix}/lib/node_modules/openclaw"
        "${npm_prefix}/node_modules/openclaw"
        "/usr/local/lib/node_modules/openclaw"
        "/usr/lib/node_modules/openclaw"
        "${HOME}/.npm-global/lib/node_modules/openclaw"
        "${HOME}/.npm-global/node_modules/openclaw"
        "/opt/openclaw"
    )

    for path in "${search_paths[@]}"; do
        if [[ -f "$path/dist/gateway/gateway-server.cjs" ]]; then
            echo "$path"
            return 0
        fi
    done

    return 1
}

# Check if npm is available
test_npm() {
    command -v npm >/dev/null 2>&1 || return 1
}

# Install OpenClaw via npm
install_openclaw() {
    log_info "Installing OpenClaw..."
    
    if ! npm install -g openclaw 2>&1 | while IFS= read -r line; do
        if [[ "$line" =~ "error" ]] || [[ "$line" =~ "ERR!" ]]; then
            log_warning "$line"
        else
            log_info "$line"
        fi
    done; then
        log_error "npm install failed"
        return 1
    fi

    log_success "OpenClaw installed successfully"
    return 0
}

# Run the patcher
invoke_patcher() {
    local openclaw_path="$1"
    local script_dir
    
    script_dir=$(cd "$(dirname "$0")" && pwd)
    local patcher_script="${script_dir}/patch-openclaw.sh"
    
    if ! [[ -f "$patcher_script" ]]; then
        log_warning "Patcher script not found at: $patcher_script"
        log_warning "Skipping patch application"
        return 1
    fi

    log_info "Applying OpenClaw patches..."
    
    if ! bash "$patcher_script" "$openclaw_path"; then
        log_error "Patcher failed"
        return 1
    fi

    return 0
}

# Parse arguments
skip_patch=false
force_install=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --skip-patch)
            skip_patch=true
            shift
            ;;
        --force)
            force_install=true
            shift
            ;;
        --help|-h)
            echo "Usage: $0 [OPTIONS]"
            echo "Options:"
            echo "  --skip-patch   Skip applying the patch after installation"
            echo "  --force        Force reinstall of OpenClaw"
            echo "  -h, --help     Show this help message"
            exit 0
            ;;
        *)
            shift
            ;;
    esac
done

# Main execution
clear
echo -e "${CYAN}╔══════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║      OpenClaw Installation & Patcher Script (Linux/macOS)        ║${NC}"
echo -e "${CYAN}║                      Version 1.0                                 ║${NC}"
echo -e "${CYAN}╚══════════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Step 1: Check Node.js
log_info "Step 1: Checking Node.js installation..."
log_info "=========================================="
echo ""

node_status=$(test_nodejs)
node_result=$?

if [[ $node_result -ne 0 ]] || [[ "$node_status" == "NOT_INSTALLED" ]]; then
    log_error "Node.js is not installed."
    log_info "Please install Node.js 18+ from https://nodejs.org/"
    exit 1
fi

if [[ "$node_status" == *"INSUFFICIENT"* ]]; then
    log_error "Node.js version must be 18 or higher"
    version_only=$(echo "$node_status" | cut -d: -f1)
    log_info "Current version: $version_only"
    log_info "Please upgrade from https://nodejs.org/"
    exit 1
fi

version_only=$(echo "$node_status" | cut -d: -f1)
log_success "Node.js found: $version_only"
log_success "Version requirement met (18+)"
echo ""

# Step 2: Check npm
log_info "Step 2: Finding npm installation..."
log_info "===================================="
echo ""

if ! test_npm; then
    log_error "npm could not be found"
    exit 1
fi

npm_version=$(npm --version 2>/dev/null || echo "unknown")
log_success "npm found: version $npm_version"

npm_prefix=$(get_npm_global_prefix)
log_info "Global prefix: $npm_prefix"
echo ""

# Step 3: Check if OpenClaw is installed
log_info "Step 3: Checking OpenClaw installation..."
log_info "=========================================="
echo ""

openclaw_path=$(test_openclaw_installed "$npm_prefix")
openclaw_result=$?

if [[ $openclaw_result -eq 0 ]] && ! [[ "$force_install" == "true" ]]; then
    log_success "OpenClaw is already installed"
    log_info "Location: $openclaw_path"
    echo ""
else
    if [[ "$force_install" == "true" ]] && [[ $openclaw_result -eq 0 ]]; then
        log_warning "Force reinstall requested"
    fi

    if ! install_openclaw; then
        log_error "Failed to install OpenClaw"
        exit 1
    fi

    # Re-check installation
    openclaw_path=$(test_openclaw_installed "$npm_prefix")
    openclaw_result=$?
    
    if [[ $openclaw_result -ne 0 ]] || [[ -z "$openclaw_path" ]]; then
        log_error "OpenClaw installation verification failed"
        exit 1
    fi

    log_success "OpenClaw installation verified"
    echo ""
fi

# Step 4: Apply patcher
if [[ "$skip_patch" != "true" ]]; then
    log_info "Step 4: Applying OpenClaw patches..."
    log_info "===================================="
    echo ""
    
    if ! invoke_patcher "$openclaw_path"; then
        log_warning "Patch application had issues, but installation is complete"
        log_info "You may need to run patch-openclaw.sh manually later"
    fi
fi

# Final summary
echo ""
echo -e "${CYAN}╔══════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║                   Installation Complete!                         ║${NC}"
echo -e "${CYAN}╚══════════════════════════════════════════════════════════════════╝${NC}"
echo ""

log_success "OpenClaw is ready to use"
log_info "Installation Path: $openclaw_path"
echo ""

log_info "Next steps:"
echo "  1. Add to PATH or use: openclaw --version"
echo "  2. Start the gateway: openclaw gateway start"
echo "  3. View status: openclaw gateway status"
echo ""

exit 0
