#!/bin/bash
# Example: Using Ollama Cloud with Ouroboros CLI
#
# This script demonstrates how to use Ollama Cloud authentication
# with all Ouroboros CLI commands.

# ========================================
# Configuration
# ========================================

# Option 1: Using Environment Variables (Recommended)
export CHAT_ENDPOINT="https://api.ollama.com"
export CHAT_API_KEY="your-ollama-cloud-api-key-here"
export CHAT_ENDPOINT_TYPE="ollama-cloud"  # or "auto" for auto-detection

# Verify configuration
echo "=== Ollama Cloud Configuration ==="
echo "Endpoint: $CHAT_ENDPOINT"
echo "API Key: ${CHAT_API_KEY:0:10}..."
echo "Endpoint Type: $CHAT_ENDPOINT_TYPE"
echo ""

# ========================================
# Example 1: Simple Question with Ask Command
# ========================================
echo "=== Example 1: Ask Command ==="
dotnet run -- ask -q "What is functional programming?" \
  --model "llama3" \
  --temperature 0.7

echo ""

# ========================================
# Example 2: RAG-enabled Question
# ========================================
echo "=== Example 2: Ask with RAG ==="
dotnet run -- ask -q "Explain the main concepts" \
  --rag \
  --model "llama3" \
  --embed "nomic-embed-text" \
  -k 5

echo ""

# ========================================
# Example 3: Agent Mode
# ========================================
echo "=== Example 3: Agent with Tool Execution ==="
dotnet run -- ask -q "Research functional programming patterns" \
  --agent \
  --agent-mode "react" \
  --agent-max-steps 5

echo ""

# ========================================
# Example 4: Pipeline Command
# ========================================
echo "=== Example 4: Pipeline DSL ==="
dotnet run -- pipeline \
  -d "SetTopic('AI Ethics') | UseDraft | UseCritique | UseImprove" \
  --model "llama3" \
  --trace

echo ""

# ========================================
# Example 5: Smart Model Orchestrator
# ========================================
echo "=== Example 5: Orchestrator ==="
dotnet run -- orchestrator \
  --goal "Write a Python function to calculate fibonacci numbers" \
  --model "llama3" \
  --coder-model "codellama" \
  --reason-model "deepseek-r1" \
  --metrics

echo ""

# ========================================
# Example 6: MeTTa Symbolic Reasoning
# ========================================
echo "=== Example 6: MeTTa Orchestrator ==="
dotnet run -- metta \
  --goal "Plan a machine learning pipeline for image classification" \
  --model "llama3" \
  --embed "nomic-embed-text" \
  --metrics

echo ""

# ========================================
# Example 7: Using CLI Flags Instead of Environment Variables
# ========================================
echo "=== Example 7: Explicit CLI Flags ==="
# This approach overrides environment variables
dotnet run -- ask -q "What are monads?" \
  --endpoint "https://api.ollama.com" \
  --api-key "$CHAT_API_KEY" \
  --endpoint-type "ollama-cloud" \
  --model "llama3"

echo ""

# ========================================
# Example 8: Multi-Model Routing
# ========================================
echo "=== Example 8: Auto-Router with Multiple Models ==="
dotnet run -- ask -q "Write and explain a sorting algorithm" \
  --router "auto" \
  --general-model "llama3" \
  --coder-model "codellama" \
  --reason-model "deepseek-r1" \
  --summarize-model "llama3"

echo ""

# ========================================
# Example 9: Plan-Only Mode (MeTTa)
# ========================================
echo "=== Example 9: MeTTa Plan-Only ==="
dotnet run -- metta \
  --goal "Design a distributed caching system" \
  --plan-only \
  --model "llama3"

echo ""

# ========================================
# Example 10: Debug Mode
# ========================================
echo "=== Example 10: Debug Mode ==="
dotnet run -- orchestrator \
  --goal "Explain category theory" \
  --debug \
  --model "llama3"

echo ""
echo "=== All Examples Complete ==="
