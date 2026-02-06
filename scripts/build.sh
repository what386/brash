#!/bin/env bash
set -euo pipefail

PROJECT_NAME="Brash"
OUTPUT_DIR="./target/release"

# Build defaults
CONFIGURATION="Release"
ENABLE_TRIMMING=true
ENABLE_SINGLE_FILE=true
ENABLE_READY_TO_RUN=true

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

show_help() {
    echo "Usage: $0 [OPTIONS] <platform1> [platform2] ..."
    echo ""
    echo "Options:"
    echo "  --no-trim          Disable trimming (faster builds, larger binaries)"
    echo "  --no-single-file   Publish as multiple files (faster builds, easier debugging)"
    echo "  --no-ready2run     Disable ReadyToRun compilation (faster builds, slower startup)"
    echo "  -h, --help         Show this help message"
    echo ""
    echo "Arguments:"
    echo "  <platforms>  Space-separated list of platforms to build"
    echo ""
    echo "Available platforms:"
    echo "  win-x64, win-x86, win-arm64"
    echo "  linux-x64, linux-arm64, linux-arm"
    echo "  osx-x64, osx-arm64"
    echo ""
    echo "Examples:"
    echo "  $0 win-x64                           # Build only Windows 64-bit"
    echo "  $0 --no-trim win-x64                 # Build Windows 64-bit without trimming"
    echo "  $0 --no-single-file --no-trim win-x64  # Fast dev build"
    echo "  $0 win-x64 linux-x64                 # Build Windows and Linux 64-bit"
    echo "  $0 osx-x64 osx-arm64                 # Build both macOS versions"
    exit 0
}

# Parse options
BUILD_PLATFORMS=()
while [[ $# -gt 0 ]]; do
    case $1 in
    -h | --help)
        show_help
        ;;
    --no-trim)
        ENABLE_TRIMMING=false
        shift
        ;;
    --no-single-file)
        ENABLE_SINGLE_FILE=false
        shift
        ;;
    --no-ready2run)
        ENABLE_READY_TO_RUN=false
        shift
        ;;
    *)
        BUILD_PLATFORMS+=("$1")
        shift
        ;;
    esac
done

# Check if any platforms specified
if [ ${#BUILD_PLATFORMS[@]} -eq 0 ]; then
    echo -e "${RED}Error: No platforms specified${NC}"
    echo ""
    show_help
fi

echo -e "${GREEN}  Building ${PROJECT_NAME}${NC}"
echo -e "${BLUE}Configuration:${NC}"
echo -e "  Trimming:     $([ "$ENABLE_TRIMMING" = true ] && echo "${GREEN}Enabled${NC}" || echo "${YELLOW}Disabled${NC}")"
echo -e "  Single File:  $([ "$ENABLE_SINGLE_FILE" = true ] && echo "${GREEN}Enabled${NC}" || echo "${YELLOW}Disabled${NC}")"
echo -e "  ReadyToRun:   $([ "$ENABLE_READY_TO_RUN" = true ] && echo "${GREEN}Enabled${NC}" || echo "${YELLOW}Disabled${NC}")"

# Clean output directory
if [ -d "$OUTPUT_DIR" ]; then
    echo -e "${YELLOW}Cleaning output directory...${NC}"
    rm -rf "$OUTPUT_DIR"
fi
mkdir -p "$OUTPUT_DIR"

get_output_filename() {
    local rid=$1
    local platform=""
    local arch=""
    local bits=""
    local extension=""

    case $rid in
    win-x64)
        platform="win"
        arch="x86"
        bits="64"
        extension=".exe"
        ;;
    win-x86)
        platform="win"
        arch="x86"
        bits="32"
        extension=".exe"
        ;;
    win-arm64)
        platform="win"
        arch="arm"
        bits="64"
        extension=".exe"
        ;;
    linux-x64)
        platform="linux"
        arch="x86"
        bits="64"
        ;;
    linux-arm64)
        platform="linux"
        arch="arm"
        bits="64"
        ;;
    linux-arm)
        platform="linux"
        arch="arm"
        bits="32"
        ;;
    osx-x64)
        platform="osx"
        arch="x86"
        bits="64"
        ;;
    osx-arm64)
        platform="osx"
        arch="arm"
        bits="64"
        ;;
    esac

    echo "${PROJECT_NAME}-${platform}_${arch}-${bits}${extension}"
}

build_platform() {
    local rid=$1
    local description=$2
    local temp_dir="$OUTPUT_DIR/temp_$rid"

    echo ""
    echo -e "${YELLOW}Building for $description ($rid)...${NC}"

    # Build publish arguments
    local publish_args=(
        ./src/$PROJECT_NAME/$PROJECT_NAME.csproj
        -r "$rid"
        --self-contained
        -p:UseAppHost=true
        -o "$temp_dir"
    )

    # Add optional features
    if [ "$ENABLE_SINGLE_FILE" = true ]; then
        publish_args+=(-p:PublishSingleFile=True)
    fi

    if [ "$ENABLE_READY_TO_RUN" = true ]; then
        publish_args+=(-p:PublishReadyToRun=True)
    fi

    if [ "$ENABLE_TRIMMING" = true ]; then
        publish_args+=(
            -p:PublishTrimmed=True
            -p:TrimMode=CopyUsed
        )
    fi

    dotnet publish "${publish_args[@]}"

    if [ $? -eq 0 ]; then
        # Get source and destination filenames
        if [[ "$rid" == win-* ]]; then
            src_file="$temp_dir/${PROJECT_NAME}.exe"
        else
            src_file="$temp_dir/${PROJECT_NAME}"
        fi

        dest_file="$OUTPUT_DIR/$(get_output_filename "$rid")"

        if [ -f "$src_file" ]; then
            # Move and rename the executable
            mv "$src_file" "$dest_file"

            # Remove temporary directory
            rm -rf "$temp_dir"

            # Get file size
            size=$(du -h "$dest_file" | cut -f1)
            echo -e "${GREEN}✓ Built successfully ($size) -> $(basename "$dest_file")${NC}"
        else
            echo -e "${RED}✗ Build completed but executable not found${NC}"
            rm -rf "$temp_dir"
        fi
    else
        echo -e "${RED}✗ Build failed${NC}"
        rm -rf "$temp_dir"
        return 1
    fi
}

get_platform_description() {
    local rid=$1
    case $rid in
    win-x64) echo "Windows (64-bit)" ;;
    win-x86) echo "Windows (32-bit)" ;;
    win-arm64) echo "Windows ARM64" ;;
    linux-x64) echo "Linux (64-bit)" ;;
    linux-arm64) echo "Linux ARM64" ;;
    linux-arm) echo "Linux ARM" ;;
    osx-x64) echo "macOS Intel" ;;
    osx-arm64) echo "macOS Apple Silicon" ;;
    *) echo "$rid" ;;
    esac
}

# Build selected platforms
echo -e "${BLUE}Building platforms: ${BUILD_PLATFORMS[*]}${NC}"

for rid in "${BUILD_PLATFORMS[@]}"; do
    description=$(get_platform_description "$rid")
    build_platform "$rid" "$description"
done

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  Build Complete!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "Executables are located in:"
echo ""

# Show output files
ls -lh "$OUTPUT_DIR"/${PROJECT_NAME}-* 2>/dev/null || echo "No executables found"
