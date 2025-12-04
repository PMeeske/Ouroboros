#!/bin/bash
# Run test coverage analysis for Ouroboros
# Usage: ./scripts/run-coverage.sh [options]
#   --no-clean    Skip cleaning previous results
#   --no-open     Don't open the report in browser
#   --minimal     Generate only text summary

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Default options
CLEAN=true
OPEN_REPORT=true
REPORT_TYPES="Html;MarkdownSummaryGithub;TextSummary"

# Parse arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --no-clean)
      CLEAN=false
      shift
      ;;
    --no-open)
      OPEN_REPORT=false
      shift
      ;;
    --minimal)
      REPORT_TYPES="TextSummary"
      OPEN_REPORT=false
      shift
      ;;
    *)
      echo "Unknown option: $1"
      echo "Usage: $0 [--no-clean] [--no-open] [--minimal]"
      exit 1
      ;;
  esac
done

echo -e "${GREEN}=== Ouroboros Test Coverage Analysis ===${NC}"
echo ""

# Clean previous results if requested
if [ "$CLEAN" = true ]; then
  echo -e "${YELLOW}Cleaning previous results...${NC}"
  rm -rf src/Ouroboros.Tests/TestResults TestCoverageReport
fi

# Check if reportgenerator is installed
if ! command -v reportgenerator &> /dev/null; then
  echo -e "${YELLOW}Installing ReportGenerator...${NC}"
  dotnet tool install -g dotnet-reportgenerator-globaltool
fi

# Run tests with coverage
echo -e "${GREEN}Running tests with code coverage...${NC}"
dotnet test \
  src/Ouroboros.Tests/Ouroboros.Tests.csproj \
  --collect:"XPlat Code Coverage" \
  --verbosity quiet \
  --nologo

# Check if coverage file was generated
COVERAGE_FILE=$(find src/Ouroboros.Tests/TestResults -name "coverage.cobertura.xml" 2>/dev/null | head -1)

if [ -z "$COVERAGE_FILE" ]; then
  echo -e "${RED}Error: Coverage file not generated${NC}"
  exit 1
fi

echo -e "${GREEN}Coverage data collected successfully${NC}"
echo ""

# Generate report
echo -e "${GREEN}Generating coverage report...${NC}"
reportgenerator \
  -reports:"src/Ouroboros.Tests/TestResults/*/coverage.cobertura.xml" \
  -targetdir:"TestCoverageReport" \
  -reporttypes:"$REPORT_TYPES" \
  -assemblyfilters:"+Ouroboros.*;+LangChainPipeline.*"

echo ""
echo -e "${GREEN}=== Coverage Summary ===${NC}"
cat TestCoverageReport/Summary.txt
echo ""

# Open report in browser if requested
if [ "$OPEN_REPORT" = true ] && [ -f "TestCoverageReport/index.html" ]; then
  echo -e "${GREEN}Opening coverage report in browser...${NC}"
  
  # Detect OS and open appropriately
  if [[ "$OSTYPE" == "darwin"* ]]; then
    open TestCoverageReport/index.html
  elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    xdg-open TestCoverageReport/index.html 2>/dev/null || echo "Please open TestCoverageReport/index.html manually"
  elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "win32" ]]; then
    start TestCoverageReport/index.html
  else
    echo "Please open TestCoverageReport/index.html manually"
  fi
fi

echo -e "${GREEN}Coverage analysis complete!${NC}"
echo "Full report available at: TestCoverageReport/index.html"
