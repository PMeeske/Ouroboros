# Operating Cost Statement Audit - Getting Started Guide

This guide provides a practical walkthrough for using the Operating Cost Statement Audit feature module in Ouroboros. The module performs **non-legal formal completeness checks** on operating cost statements (Betriebskostenabrechnungen).

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Recommended Model Setup](#recommended-model-setup)
4. [Document Ingestion](#document-ingestion)
5. [Quick Start](#quick-start)
6. [Step-by-Step Usage](#step-by-step-usage)
7. [Understanding the Output](#understanding-the-output)
8. [Advanced Configuration](#advanced-configuration)
9. [Troubleshooting](#troubleshooting)

---

## Overview

### What This Module Does

The Operating Cost Statement Audit module analyzes rental operating cost statements for **formal completeness** and **traceability**, checking whether the following minimum data points are clearly visible:

1. **Total costs per cost category** (not reconstructed from attachments)
2. **Declared allocation key** (e.g., living area, ownership shares/MEA, occupants)
3. **Total reference metric** (e.g., total living area, total units)
4. **Allocated share for the tenant** (percentage, physical share, or fraction)
5. **Calculated cost portion** attributable to the tenant
6. **Deducted advance payments**
7. **Resulting balance** (credit or amount due)

### What This Module Does NOT Do

- ❌ Provide legal advice
- ❌ Assess enforceability or validity
- ❌ Make statements about deadlines or payment obligations
- ❌ Interpret legal consequences

---

## Prerequisites

### 1. Ouroboros Installation

Ensure you have Ouroboros installed and built:

```bash
# Clone the repository
git clone https://github.com/PMeeske/Ouroboros.git
cd Ouroboros

# Build the solution
dotnet build Ouroboros.sln
```

### 2. LLM Provider Setup

You need an LLM provider. We recommend **Ollama** for local deployment:

```bash
# Install Ollama (Linux/macOS)
curl -fsSL https://ollama.com/install.sh | sh

# Start Ollama service
ollama serve
```

### 3. Required Models

Pull the recommended models:

```bash
# Primary model for analysis (recommended)
ollama pull llama3.1:8b

# Embedding model for document vectorization
ollama pull nomic-embed-text

# Alternative: Smaller model for faster processing
ollama pull phi3:mini

# Alternative: German-optimized (if available)
ollama pull mixtral:8x7b
```

---

## Recommended Model Setup

### For Best Accuracy (Recommended)

| Use Case | Model | Size | Notes |
|----------|-------|------|-------|
| **Primary Analysis** | `llama3.1:8b` | 4.7GB | Best balance of quality and speed |
| **Document Embedding** | `nomic-embed-text` | 274MB | Fast, high-quality embeddings |
| **Complex Documents** | `llama3.1:70b` | 40GB | Highest accuracy, requires GPU |
| **German Documents** | `mixtral:8x7b` | 26GB | Better multilingual support |

### For Resource-Constrained Systems

| Use Case | Model | Size | Notes |
|----------|-------|------|-------|
| **Primary Analysis** | `phi3:mini` | 2.3GB | Fast, good for simple statements |
| **Document Embedding** | `nomic-embed-text` | 274MB | Still recommended |
| **Quick Checks** | `qwen2.5:3b` | 2GB | Very fast, basic analysis |

### Configuration Example

```csharp
using LangChain.Providers.Ollama;
using Ouroboros.Pipeline.Reasoning;
using Ouroboros.Tools;

// Setup Ollama provider
var provider = new OllamaProvider();

// Create chat model adapter
var chatModel = new OllamaChatModel(provider, "llama3.1:8b");
var adapter = new OllamaChatAdapter(chatModel);

// Create embedding model for document ingestion
var embedModel = new OllamaEmbeddingModel(provider, "nomic-embed-text");
var embedAdapter = new OllamaEmbeddingAdapter(embedModel);

// Create tool-aware model
var tools = ToolRegistry.CreateDefault();
var llm = new ToolAwareChatModel(adapter, tools);
```

---

## Document Ingestion

Before running the audit, you need to ingest your documents into the system. Ouroboros supports multiple document formats and ingestion methods.

### Supported Document Formats

| Format | Extension | Best For |
|--------|-----------|----------|
| **Plain Text** | `.txt` | Simple statements, extracted text |
| **PDF** | `.pdf` | Scanned or digital statements |
| **Markdown** | `.md` | Structured documents |
| **Word** | `.docx` | Office documents |
| **HTML** | `.html` | Web-exported statements |

### Method 1: Direct Text Ingestion (Simplest)

For quick analysis when you already have the text content:

```csharp
using Ouroboros.Pipeline.Branches;
using Ouroboros.Domain.Vectors;
using LangChain.DocumentLoaders;

// Your operating cost statement as text
string mainStatement = @"
Betriebskostenabrechnung 2023
Abrechnungszeitraum: 01.01.2023 - 31.12.2023
Mieter: Max Mustermann
Wohnung: Musterstraße 1, 12345 Berlin, 75 qm

=== Heizkosten ===
Gesamtkosten Gebäude: 5.000,00 EUR
Verteilerschlüssel: Wohnfläche
Gesamtwohnfläche: 500 qm
Ihr Anteil (75/500 = 15%): 750,00 EUR

=== Wasserkosten ===
Gesamtkosten: 2.000,00 EUR
Ihr Anteil: 300,00 EUR

Summe Betriebskosten: 1.050,00 EUR
Vorauszahlungen: 900,00 EUR
Nachzahlung: 150,00 EUR
";

// Create vector store and branch
var store = new TrackedVectorStore();
var branch = new PipelineBranch("audit-2023", store, DataSource.FromPath("."));

// Optionally add the statement to vector store for RAG
await store.AddDocument(mainStatement, embedAdapter);
```

### Method 2: Single File Ingestion

Load a document from a single file:

```csharp
using LangChain.DocumentLoaders;
using LangChain.Splitters.Text;
using Ouroboros.Pipeline.Ingestion;

// Setup embedding model
var provider = new OllamaProvider();
var embedModel = new OllamaEmbeddingModel(provider, "nomic-embed-text");
var embedAdapter = new OllamaEmbeddingAdapter(embedModel);

// Create vector store
var store = new TrackedVectorStore();

// Load a single text file
var textLoader = new TextLoader();
var documents = await textLoader.LoadAsync(
    DataSource.FromPath("/path/to/betriebskostenabrechnung.txt")
);

// Split into chunks and add to store
var splitter = new RecursiveCharacterTextSplitter(chunkSize: 2000, chunkOverlap: 200);
foreach (var doc in documents)
{
    var chunks = splitter.SplitText(doc.PageContent);
    foreach (var chunk in chunks)
    {
        await store.AddDocument(chunk, embedAdapter);
    }
}

// Create branch with ingested documents
var branch = new PipelineBranch("audit-2023", store, DataSource.FromPath("."));
```

### Method 3: PDF Document Ingestion

For PDF operating cost statements (common format):

```csharp
using LangChain.DocumentLoaders;

// Note: Requires LangChain PDF support package
// dotnet add package LangChain.DocumentLoaders.Pdf

var pdfLoader = new PdfPigLoader(); // or other PDF loader
var documents = await pdfLoader.LoadAsync(
    DataSource.FromPath("/path/to/abrechnung.pdf")
);

// Process the loaded documents
string fullText = string.Join("\n\n", documents.Select(d => d.PageContent));
Console.WriteLine($"Loaded {documents.Count} pages from PDF");

// Add to vector store
var splitter = new RecursiveCharacterTextSplitter(chunkSize: 2000, chunkOverlap: 200);
var chunks = splitter.SplitText(fullText);
foreach (var chunk in chunks)
{
    await store.AddDocument(chunk, embedAdapter);
}
```

### Method 4: Directory Ingestion (Multiple Documents)

Load all documents from a directory (useful for complete audit packages):

```csharp
using Ouroboros.Pipeline.Ingestion;
using LangChain.DocumentLoaders;

// Configure directory ingestion options
var options = new DirectoryIngestionOptions
{
    Recursive = true,                              // Include subdirectories
    Extensions = new[] { ".txt", ".md", ".pdf" },  // File types to include
    ExcludeDirectories = new[] { "archive", "backup" },
    MaxFileBytes = 10 * 1024 * 1024,              // 10 MB max per file
    ChunkSize = 2000,
    ChunkOverlap = 200
};

// Create directory loader
var dirLoader = new DirectoryDocumentLoader<TextLoader>(options);

// Load all documents from folder
var documents = await dirLoader.LoadAsync(
    DataSource.FromPath("/path/to/audit-documents/")
);

Console.WriteLine($"Loaded {documents.Count} document chunks");

// Add all to vector store
foreach (var doc in documents)
{
    if (!string.IsNullOrWhiteSpace(doc.PageContent))
    {
        await store.AddDocument(doc.PageContent, embedAdapter);
    }
}
```

### Method 5: Using Ingestion Arrows (Pipeline Integration)

For seamless pipeline integration:

```csharp
using Ouroboros.Pipeline.Ingestion;
using Ouroboros.Pipeline.Branches;

// Create initial branch pointing to documents directory
var store = new TrackedVectorStore();
var branch = new PipelineBranch(
    "audit-2023",
    store,
    DataSource.FromPath("/path/to/audit-documents/")
);

// Create ingestion arrow
var ingestArrow = IngestionArrows.IngestArrow<TextLoader>(
    embedAdapter,
    splitter: new RecursiveCharacterTextSplitter(chunkSize: 2000, chunkOverlap: 200),
    tag: "operating-cost-docs"
);

// Execute ingestion
var ingestedBranch = await ingestArrow(branch);

// Now run the audit on the ingested branch
var auditPipeline = OperatingCostAuditArrows.SafeBasicAuditPipeline(
    llm,
    tools,
    mainStatementText  // Still need the main statement text for analysis
);

var result = await auditPipeline(ingestedBranch);
```

### Ingesting Multiple Document Types

For a complete audit package with different document types:

```csharp
// Structure your audit folder like this:
// /audit-2023/
//   ├── hauptabrechnung.txt      (Main statement)
//   ├── weg-abrechnung.txt       (HOA/WEG statement)
//   ├── mietvertrag-auszug.txt   (Rental agreement excerpts)
//   ├── belege/                  (Receipts folder)
//   │   ├── heizkosten.pdf
//   │   └── wasserkosten.pdf
//   └── protokolle/              (Protocols folder)
//       └── ableseprotokoll.txt

// Load main statement
string mainStatement = await File.ReadAllTextAsync("/audit-2023/hauptabrechnung.txt");

// Load HOA statement (optional)
string? hoaStatement = File.Exists("/audit-2023/weg-abrechnung.txt")
    ? await File.ReadAllTextAsync("/audit-2023/weg-abrechnung.txt")
    : null;

// Load rental agreement rules (optional)
string? rentalRules = File.Exists("/audit-2023/mietvertrag-auszug.txt")
    ? await File.ReadAllTextAsync("/audit-2023/mietvertrag-auszug.txt")
    : null;

// Ingest all supporting documents into vector store for context
var dirLoader = new DirectoryDocumentLoader<TextLoader>(new DirectoryIngestionOptions
{
    Recursive = true,
    Extensions = new[] { ".txt", ".md" }
});

var supportDocs = await dirLoader.LoadAsync(DataSource.FromPath("/audit-2023/"));
foreach (var doc in supportDocs)
{
    await store.AddDocument(doc.PageContent, embedAdapter);
}

// Run full audit with all documents
var fullPipeline = OperatingCostAuditArrows.FullAuditPipeline(
    llm,
    tools,
    mainStatement,
    hoaStatement,
    rentalRules
);

var result = await fullPipeline(branch);
```

### Text Extraction from Scanned Documents

For scanned PDFs, you'll need OCR. Here's a recommended workflow:

```bash
# Option 1: Use Tesseract OCR (command line)
tesseract scanned-statement.pdf output -l deu pdf

# Option 2: Use online OCR services before ingestion

# Option 3: Use Adobe Acrobat or similar to export as text
```

Then ingest the extracted text:

```csharp
// After OCR extraction
string ocrText = await File.ReadAllTextAsync("output.txt");

// Clean up common OCR artifacts
ocrText = ocrText
    .Replace("€", "EUR")           // Fix currency symbols
    .Replace("  ", " ")            // Remove double spaces
    .Replace("\n\n\n", "\n\n");    // Normalize line breaks

await store.AddDocument(ocrText, embedAdapter);
```

### Best Practices for Document Ingestion

1. **Clean your text**: Remove headers, footers, and page numbers that repeat
2. **Use appropriate chunk sizes**: 
   - 2000 characters for detailed analysis
   - 4000 characters for overview analysis
3. **Preserve structure**: Keep section headers and formatting where possible
4. **Label documents**: Use metadata to track document sources
5. **Handle German characters**: Ensure UTF-8 encoding for umlauts (ä, ö, ü, ß)

```csharp
// Example: Adding metadata to documents
var metadata = new Dictionary<string, object>
{
    ["documentType"] = "hauptabrechnung",
    ["year"] = "2023",
    ["source"] = "landlord",
    ["language"] = "de"
};

await store.AddDocument(mainStatement, embedAdapter, metadata);
```

---

## Quick Start

### Minimal Example

```csharp
using Ouroboros.Pipeline.Reasoning;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Domain.Vectors;
using Ouroboros.Tools;
using LangChain.Providers.Ollama;
using LangChain.DocumentLoaders;

// 1. Setup LLM
var provider = new OllamaProvider();
var chatModel = new OllamaChatModel(provider, "llama3.1:8b");
var adapter = new OllamaChatAdapter(chatModel);
var tools = ToolRegistry.CreateDefault();
var llm = new ToolAwareChatModel(adapter, tools);

// 2. Prepare your operating cost statement
string mainStatement = @"
Betriebskostenabrechnung 2023
Mieter: Max Mustermann
Wohnung: Musterstraße 1, 12345 Berlin

Heizkosten:
- Gesamtkosten: 5.000,00 EUR
- Ihr Anteil (75 qm / 500 qm): 750,00 EUR

Wasserkosten:
- Gesamtkosten: 2.000,00 EUR  
- Ihr Anteil: 300,00 EUR

Vorauszahlungen: 900,00 EUR
Nachzahlung: 150,00 EUR
";

// 3. Create pipeline branch
var store = new TrackedVectorStore();
var branch = new PipelineBranch("audit-2023", store, DataSource.FromPath("."));

// 4. Run the basic audit pipeline
var pipeline = OperatingCostAuditArrows.SafeBasicAuditPipeline(llm, tools, mainStatement);
var result = await pipeline(branch);

// 5. Process the result
result.Match(
    success => {
        var finalSpec = success.Events
            .OfType<ReasoningStep>()
            .LastOrDefault()?.State;
        Console.WriteLine("Audit Result:");
        Console.WriteLine(finalSpec?.Text);
    },
    error => Console.WriteLine($"Audit failed: {error}")
);
```

---

## Step-by-Step Usage

### Step 1: Prepare Your Documents

Gather the following documents:

| Document | Required | Description |
|----------|----------|-------------|
| **Main Statement** | ✅ Yes | The operating cost statement (Betriebskostenabrechnung) |
| **HOA/WEG Statement** | ⬜ Optional | Condominium association cost breakdown |
| **Rental Agreement** | ⬜ Optional | Passages about allocation rules |
| **Receipts/Invoices** | ⬜ Optional | Individual cost documentation |

### Step 2: Choose Your Pipeline

#### Option A: Basic Audit (Main Statement Only)

```csharp
// Simple audit checking only the main statement
var pipeline = OperatingCostAuditArrows.SafeBasicAuditPipeline(
    llm, 
    tools, 
    mainStatement
);
```

#### Option B: Full Audit (With Comparisons)

```csharp
// Full audit with HOA comparison and allocation rule check
var pipeline = OperatingCostAuditArrows.FullAuditPipeline(
    llm,
    tools,
    mainStatement,
    hoaStatement: wegStatement,           // Optional: WEG/HOA breakdown
    rentalAgreementRules: allocationRules // Optional: §-passages from contract
);
```

### Step 3: Execute the Pipeline

```csharp
// Create a pipeline branch to track the audit
var store = new TrackedVectorStore();
var branch = new PipelineBranch(
    "audit-" + DateTime.Now.ToString("yyyy-MM-dd"),
    store,
    DataSource.FromPath(".")
);

// Execute
var result = await pipeline(branch);
```

### Step 4: Extract and Use the Results

```csharp
result.Match(
    success => {
        // Get all reasoning steps
        var steps = success.Events.OfType<ReasoningStep>().ToList();
        
        // The final report is in the last FinalSpec state
        var finalReport = steps
            .Select(s => s.State)
            .OfType<FinalSpec>()
            .LastOrDefault();
            
        if (finalReport != null)
        {
            Console.WriteLine("=== AUDIT REPORT ===");
            Console.WriteLine(finalReport.Text);
        }
        
        // Get any critiques (comparisons, rule checks)
        var critiques = steps
            .Select(s => s.State)
            .OfType<Critique>()
            .ToList();
            
        foreach (var critique in critiques)
        {
            Console.WriteLine("\n=== CRITIQUE ===");
            Console.WriteLine(critique.Text);
        }
    },
    error => Console.WriteLine($"Error: {error}")
);
```

---

## Understanding the Output

### Field Status Values

| Status | Meaning | Action Needed |
|--------|---------|---------------|
| **OK** | Directly visible on main statement | ✅ None |
| **INDIRECT** | Only derivable from attachments | ⚠️ Request clarification |
| **UNCLEAR** | Present but not properly labeled | ⚠️ Request specification |
| **MISSING** | Not provided anywhere | ❌ Critical gap |
| **INCONSISTENT** | Conflicting data between documents | ❌ Request correction |

### Overall Status Values

| Status | Meaning |
|--------|---------|
| **Complete** | All required fields are OK |
| **Incomplete** | Some fields are MISSING, UNCLEAR, or INDIRECT |
| **NotAuditable** | Critical information missing; cannot perform meaningful audit |

### Example JSON Output

```json
{
  "documents_analyzed": true,
  "overall_formal_status": "incomplete",
  "categories": [
    {
      "category": "heating",
      "total_costs": "OK",
      "reference_metric": "UNCLEAR",
      "total_reference_value": "OK",
      "tenant_share": "OK",
      "tenant_cost": "OK",
      "balance": "OK",
      "comment": "Reference metric shows '75 qm / 500 qm' but not labeled as living area"
    },
    {
      "category": "water",
      "total_costs": "OK",
      "reference_metric": "MISSING",
      "total_reference_value": "MISSING",
      "tenant_share": "INDIRECT",
      "tenant_cost": "OK",
      "balance": "OK",
      "comment": "No allocation key specified for water costs"
    }
  ],
  "critical_gaps": [
    "Reference metric not clearly labeled as living area/MEA/units",
    "Water costs: allocation key not specified",
    "Water costs: total reference value missing"
  ],
  "summary_short": "The statement is incomplete. Heating costs lack clear reference metric labeling. Water costs are missing allocation key and total reference value.",
  "note": "This output does not contain legal evaluation or statements on validity or enforceability."
}
```

---

## Advanced Configuration

### Custom Tool Registry

Add specialized tools for your audit workflow:

```csharp
var tools = ToolRegistry.CreateDefault()
    .WithTool(new MathTool())  // For calculation verification
    .WithFunction(
        "percentage_check",
        "Verifies percentage calculations",
        (string input) => {
            // Custom percentage validation logic
            return "Calculation verified";
        }
    );
```

### Using with Vector Store for Context

```csharp
// Load previous years' statements for comparison
var store = new TrackedVectorStore();

// Add historical context
var embedModel = new OllamaEmbeddingAdapter(
    new OllamaEmbeddingModel(provider, "nomic-embed-text")
);

await store.AddDocument(
    "Previous year statement content...",
    embedModel,
    metadata: new Dictionary<string, string> { ["year"] = "2022" }
);
```

### Streaming Results (Real-time Feedback)

For long documents, use streaming to see progress:

```csharp
// Note: Streaming requires IStreamingChatModel implementation
// This is an advanced feature for real-time UI updates
```

---

## Troubleshooting

### Common Issues

#### "Connection refused" Error

```
⚠️ Ollama is not running
```

**Solution:**
```bash
# Start Ollama service
ollama serve

# Verify it's running
curl http://localhost:11434/api/tags
```

#### Model Not Found

```
Error: model 'llama3.1:8b' not found
```

**Solution:**
```bash
# Pull the required model
ollama pull llama3.1:8b

# List available models
ollama list
```

#### Out of Memory

```
Error: CUDA out of memory
```

**Solution:**
- Use a smaller model: `phi3:mini` or `qwen2.5:3b`
- Or run on CPU: Set `OLLAMA_NUM_GPU=0`

#### Empty or Poor Results

**Possible causes:**
1. Document text is too short or unclear
2. Model doesn't understand the language
3. Document format is unusual

**Solutions:**
- Ensure document text is clean (no OCR errors)
- Try a larger model for complex documents
- For German documents, consider `mixtral:8x7b`

### Performance Tips

1. **For faster processing**: Use `phi3:mini` for initial screening
2. **For accuracy**: Use `llama3.1:8b` or larger
3. **For German documents**: Consider multilingual models
4. **For batch processing**: Reuse the same `ToolAwareChatModel` instance

---

## Next Steps

- Review the [API Reference](../api/OperatingCostAudit.md) for detailed method documentation
- Check [Example Scenarios](../examples/OperatingCostAuditExamples.md) for real-world use cases
- See [Integration Guide](../integration/OperatingCostAuditIntegration.md) for embedding in your application

---

## Legal Disclaimer

This module performs **formal completeness checks only**. It does not:
- Provide legal advice
- Assess the legal validity of operating cost statements
- Make determinations about enforceability
- Replace professional legal counsel

Always consult with a qualified legal professional for legal matters related to operating cost statements.
