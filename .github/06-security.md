# 6 – Security Hardening & Secrets Management   <!-- Issue #6 -->

**Goal:** Secure the codebase, CI, and runtime environment.

## Tasks
- [ ] Perform dependency-scans (OWASP, Snyk)
- [ ] Use GitHub OIDC + Vault (or Azure Key Vault) for secrets
- [ ] Enable branch protection + signed commits
- [ ] Threat-model the pipeline against prompt injection

## Acceptance Criteria
- Zero high/critical CVEs
- Secrets never stored in repo or CI logs
- Threat model document in `/docs/security/`

_Part of #1_