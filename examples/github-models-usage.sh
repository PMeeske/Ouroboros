#!/bin/bash
# GitHub Models Usage Examples
# This script demonstrates how to use Ouroboros with GitHub Models API

# Prerequisites:
# 1. Obtain a GitHub Personal Access Token from https://github.com/settings/tokens
# 2. Set the GITHUB_TOKEN environment variable
# 3. Build the Ouroboros CLI project

set -e

# Configuration
export GITHUB_TOKEN="${GITHUB_TOKEN:-your-github-token-here}"
export CHAT_ENDPOINT="https://models.inference.ai.azure.com"
export CHAT_ENDPOINT_TYPE="github-models"

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}======================================${NC}"
echo -e "${BLUE}GitHub Models Usage Examples${NC}"
echo -e "${BLUE}======================================${NC}"
echo ""

# Check if token is set
if [ "$GITHUB_TOKEN" = "your-github-token-here" ]; then
    echo -e "${YELLOW}Warning: GITHUB_TOKEN is not set. Please set it before running this script.${NC}"
    echo "  export GITHUB_TOKEN=\"ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\""
    exit 1
fi

# Navigate to CLI directory
cd "$(dirname "$0")/../src/Ouroboros.CLI" || exit 1

echo -e "${GREEN}Example 1: Simple question with GPT-4o${NC}"
echo "Command: dotnet run -- ask -q \"What is functional programming?\" --model gpt-4o"
echo ""
dotnet run -- ask -q "What is functional programming?" --model gpt-4o
echo ""

echo -e "${GREEN}Example 2: Code generation with Claude 3.5 Sonnet${NC}"
echo "Command: dotnet run -- ask -q \"Write a Python function to calculate Fibonacci\" --model claude-3-5-sonnet"
echo ""
dotnet run -- ask -q "Write a Python function to calculate Fibonacci numbers" --model claude-3-5-sonnet
echo ""

echo -e "${GREEN}Example 3: Reasoning with o1-preview${NC}"
echo "Command: dotnet run -- ask -q \"Explain monads in category theory\" --model o1-preview"
echo ""
dotnet run -- ask -q "Explain monads in category theory" --model o1-preview
echo ""

echo -e "${GREEN}Example 4: Fast responses with GPT-4o-mini${NC}"
echo "Command: dotnet run -- ask -q \"What is a monad?\" --model gpt-4o-mini"
echo ""
dotnet run -- ask -q "What is a monad?" --model gpt-4o-mini
echo ""

echo -e "${GREEN}Example 5: Using Llama 3.1 70B${NC}"
echo "Command: dotnet run -- ask -q \"Explain event sourcing\" --model llama-3.1-70b-instruct"
echo ""
dotnet run -- ask -q "Explain event sourcing in distributed systems" --model llama-3.1-70b-instruct
echo ""

echo -e "${GREEN}Example 6: Pipeline with GitHub Models${NC}"
echo "Command: dotnet run -- pipeline -d \"SetTopic('Functional Programming') | UseDraft\" --model gpt-4o"
echo ""
dotnet run -- pipeline -d "SetTopic('Functional Programming') | UseDraft" --model gpt-4o
echo ""

echo -e "${GREEN}Example 7: Orchestrator with GitHub Models${NC}"
echo "Command: dotnet run -- orchestrator --goal \"Explain category theory\" --model gpt-4o"
echo ""
dotnet run -- orchestrator --goal "Explain category theory for beginners" --model gpt-4o
echo ""

echo -e "${GREEN}Example 8: Using custom endpoint and settings${NC}"
echo "Command with temperature and max-tokens settings"
echo ""
dotnet run -- ask \
  -q "Write a short story about AI" \
  --model gpt-4o \
  --temperature 0.9 \
  --max-tokens 500 \
  --endpoint "https://models.inference.ai.azure.com" \
  --endpoint-type github-models
echo ""

echo -e "${BLUE}======================================${NC}"
echo -e "${BLUE}All examples completed!${NC}"
echo -e "${BLUE}======================================${NC}"
echo ""
echo "Tip: Set GITHUB_TOKEN in your shell profile for persistent access:"
echo "  echo 'export GITHUB_TOKEN=\"ghp_xxx\"' >> ~/.bashrc"
echo ""
echo "Available models:"
echo "  - gpt-4o, gpt-4o-mini (OpenAI)"
echo "  - o1-preview, o1-mini (OpenAI reasoning models)"
echo "  - claude-3-5-sonnet, claude-3-haiku (Anthropic)"
echo "  - llama-3.1-70b-instruct, llama-3.1-405b-instruct (Meta)"
echo "  - mistral-large, mistral-small (Mistral)"
echo ""
