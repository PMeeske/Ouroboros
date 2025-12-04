# Ouroboros Documentation

This directory contains comprehensive technical documentation for Ouroboros.

## Quick Navigation

### 🚀 Getting Started
- [Main README](../README.md) - Project overview, features, and quick start
- [Deployment Quick Reference](../DEPLOYMENT-QUICK-REFERENCE.md) - Common deployment commands
- [Troubleshooting](../TROUBLESHOOTING.md) - Common issues and solutions

### 📦 Deployment & Infrastructure

#### Deployment Guides
- [**Deployment Guide**](../DEPLOYMENT.md) - Comprehensive deployment guide for all environments
- [**IONOS Deployment Guide**](IONOS_DEPLOYMENT_GUIDE.md) - Detailed IONOS Cloud deployment
- [**IONOS IaC Quick Start**](IONOS_IAC_QUICKSTART.md) - Quick start for Infrastructure as Code
- [**IONOS IaC Guide**](IONOS_IAC_GUIDE.md) - Complete Infrastructure as Code guide
- [**IONOS IaC Example**](IONOS_IAC_EXAMPLE.md) - Example IaC configurations

#### Infrastructure Documentation
- [**Infrastructure Dependencies**](INFRASTRUCTURE_DEPENDENCIES.md) - Complete dependency mapping across C#, K8s, and Terraform
- [**Terraform-Kubernetes Integration**](TERRAFORM_K8S_INTEGRATION.md) - Integration patterns and workflows
- [**Environment Infrastructure Mapping**](ENVIRONMENT_INFRASTRUCTURE_MAPPING.md) - Environment-specific configurations
- [**Deployment Topology**](DEPLOYMENT_TOPOLOGY.md) - Visual topological representations
- [**Infrastructure Migration Guide**](INFRASTRUCTURE_MIGRATION_GUIDE.md) - Safe migration procedures
- [**Infrastructure Runbook**](INFRASTRUCTURE_RUNBOOK.md) - Incident response procedures

#### Configuration
- [**Configuration and Security**](../CONFIGURATION_AND_SECURITY.md) - Security best practices and configuration
- [**Environment Detection**](ENVIRONMENT_DETECTION.md) - Runtime environment detection
- [**External Access Validation**](EXTERNAL_ACCESS_VALIDATION.md) - External access configuration
- [**Kubernetes Version Compatibility**](K8S_VERSION_COMPATIBILITY.md) - K8s version compatibility matrix

### 🧠 Architecture & Features

#### Core Architecture
- [**Architectural Layer Diagram**](ARCHITECTURAL_LAYERS.md) - Comprehensive visual system architecture with component diagrams, data flow, and deployment topology
- [**Architecture Documentation**](ARCHITECTURE.md) - Detailed system architecture
- [**Self-Improving Agent**](SELF_IMPROVING_AGENT.md) - Self-improving agent architecture and capabilities
- [**Iterative Refinement Architecture**](ITERATIVE_REFINEMENT_ARCHITECTURE.md) - Refinement loop architecture
- [**Recursive Chunking**](RECURSIVE_CHUNKING.md) - Large context processing with adaptive chunking

#### Specifications & Requirements
- [**Non-Functional Requirements (NFRs)**](specs/v1.0-nfr.md) - v1.0 NFRs with 9 categories (Performance, Security, Reliability, Scalability, Compatibility, Maintainability, Observability, Resource Efficiency, Compliance)

#### Phase Implementation
- [**Phase 2 Implementation Summary**](PHASE2_IMPLEMENTATION_SUMMARY.md) - Metacognition features
- [**Phase 3 Implementation Summary**](PHASE3_IMPLEMENTATION_SUMMARY.md) - Emergent intelligence features

### 🧪 Testing & Quality

- [**Test Coverage Report**](../TEST_COVERAGE_REPORT.md) - Detailed test coverage analysis
- [**Test Coverage Quick Reference**](../TEST_COVERAGE_QUICKREF.md) - Testing commands and metrics

### 📊 Status & Baselines

- [**Current State Baseline**](status/baseline.md) - Comprehensive baseline inventory for production-ready initiative (Epic #120)

### 👥 Contributing & Development

- [**Contributing Guide**](CONTRIBUTING.md) - Guidelines for contributing to the project
- [**GitHub Copilot Development Loop**](COPILOT_DEVELOPMENT_LOOP.md) - Automated development workflows with AI assistance
- [**Automated Development Cycle**](AUTOMATED_DEVELOPMENT_CYCLE.md) - Fully automated code improvement workflow
- [**Playwright Copilot Assignment**](PLAYWRIGHT_COPILOT_ASSIGNMENT.md) - UI-based automation for GitHub issue assignment 🎭
- [**Architecture Documentation**](ARCHITECTURE.md) - Detailed system architecture
- [**Epic #120 Integration Guide**](Epic120Integration.md) - Epic branch orchestration and v1.0 release coordination
- [**Feature Engineering**](FEATURE_ENGINEERING.md) - Feature extraction and engineering approaches

### 📚 Historical Documentation

The [archive/](archive/) directory contains historical implementation summaries, incident reports, and completed work documentation. These serve as reference material for understanding past decisions but may contain outdated information.

## Documentation Organization

### Root Directory (`/`)
Contains user-facing documentation:
- `README.md` - Main project documentation
- `DEPLOYMENT.md` - Deployment guide
- `DEPLOYMENT-QUICK-REFERENCE.md` - Quick command reference
- `TROUBLESHOOTING.md` - Troubleshooting guide
- `CONFIGURATION_AND_SECURITY.md` - Configuration guide
- `TEST_COVERAGE_REPORT.md` - Test coverage report
- `TEST_COVERAGE_QUICKREF.md` - Testing quick reference

### Docs Directory (`/docs`)
Contains detailed technical documentation:
- **Infrastructure**: Deployment topology, dependencies, migrations
- **Architecture**: Agent features, processing strategies
- **Guides**: Platform-specific deployment guides (IONOS, etc.)
- **Implementation**: Phase summaries and feature documentation
- **Status**: Baseline assessments and progress tracking

### Archive Directory (`/docs/archive`)
Contains historical documentation:
- Implementation summaries (phases, features)
- Incident reports and resolutions
- Sprint summaries
- Deprecated guides

## Contributing to Documentation

When adding new documentation:

1. **User-facing guides** → Root directory
2. **Technical deep-dives** → `/docs` directory
3. **Historical records** → `/docs/archive` directory
4. **Update this index** when adding new docs
5. **Cross-reference** related documents
6. **Follow markdown best practices**

## Documentation Standards

- Use clear, descriptive titles
- Include a table of contents for long documents
- Provide code examples where applicable
- Keep examples up-to-date with current codebase
- Use consistent formatting and style
- Link to related documentation

---

**Need help?** Start with the [Main README](../README.md) or check [Troubleshooting](../TROUBLESHOOTING.md).
