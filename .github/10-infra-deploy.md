# 10 – Deployment & Infrastructure as Code   <!-- Issue #10 -->

**Goal:** Enable repeatable, audited deployments.

## Tasks
- [ ] Write Terraform / Bicep modules for target cloud
- [ ] Containerise services (Docker)
- [ ] Provide Helm chart for Kubernetes

## Acceptance Criteria
- `terraform apply` provisions staging in < 15 min
- Supports green/blue deployments

_Part of #1_