#!/bin/bash
# Local deployment script for MonadicPipeline
# Publishes the application as a self-contained executable
# Usage: ./deploy-local.sh [output-dir]

set -e

OUTPUT_DIR="${1:-./publish}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo "================================================"
echo "MonadicPipeline Local Deployment"
echo "================================================"
echo "Output Directory: $OUTPUT_DIR"
echo ""

cd "$PROJECT_ROOT"

# Clean previous publish
if [ -d "$OUTPUT_DIR" ]; then
    echo "Cleaning previous publish..."
    rm -rf "$OUTPUT_DIR"
fi

# Publish as self-contained
echo "Step 1: Publishing application..."
dotnet publish src/MonadicPipeline.CLI/MonadicPipeline.CLI.csproj \
    -c Release \
    -o "$OUTPUT_DIR" \
    --self-contained false \
    /p:PublishSingleFile=false

if [ $? -ne 0 ]; then
    echo "Error: Publish failed"
    exit 1
fi

echo "✓ Application published successfully"
echo ""

# Copy configuration files
echo "Step 2: Copying configuration files..."
cp appsettings.json "$OUTPUT_DIR/"
cp appsettings.Production.json "$OUTPUT_DIR/"
cp appsettings.Development.json "$OUTPUT_DIR/"

echo "✓ Configuration files copied"
echo ""

# Create logs directory
mkdir -p "$OUTPUT_DIR/logs"

echo "================================================"
echo "Deployment Complete!"
echo "================================================"
echo ""
echo "Application published to: $OUTPUT_DIR"
echo ""
echo "Run the application:"
echo "  cd $OUTPUT_DIR"
echo "  dotnet LangChainPipeline.dll --help"
echo ""
echo "Or create a system service (systemd):"
echo "  sudo cp scripts/monadic-pipeline.service /etc/systemd/system/"
echo "  sudo systemctl enable monadic-pipeline"
echo "  sudo systemctl start monadic-pipeline"
echo ""
