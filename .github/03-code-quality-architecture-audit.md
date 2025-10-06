# 3 – Code Quality & Architecture Audit   <!-- Issue #3 -->

**Goal:** Ensure the codebase adheres to C# and functional-programming best practices.

## Tasks
- [ ] Run static analyzers (Roslyn, ReSharper, etc.)
- [ ] Enforce nullable reference types; treat warnings as errors
- [ ] Break cyclic dependencies; review layering
- [ ] Adopt `Result<T>` / `Option<T>` consistently
- [ ] Record architectural decisions in `/docs/adr/`

## Acceptance Criteria
- `dotnet build -warnaserror` passes
- Code coverage ≥ 90 %
- No high-severity static-analysis findings

_Part of #1_