# IONOS Cloud API Specification

This directory contains the OpenAPI specification for IONOS Cloud API.

## Files

- **ionos-cloud-api-spec.yaml** - OpenAPI 3.0.3 specification for IONOS Cloud API v6

## About IONOS Cloud API

IONOS Enterprise-grade Infrastructure as a Service (IaaS) solutions can be managed through the Cloud API, in addition or as an alternative to the "Data Center Designer" (DCD) browser-based tool.

### API Base URL
```
https://api.ionos.com/cloudapi/v6
```

### Documentation
- Official IONOS Cloud API Documentation: https://api.ionos.com/docs/
- Swagger UI: Available at IONOS Cloud Console

## Usage in Ouroboros

This API specification is referenced by:
- `.github/workflows/terraform-infrastructure.yml` - Infrastructure provisioning
- `.github/workflows/ionos-deploy.yml` - Kubernetes deployment

The Terraform configurations in `terraform/` directory utilize this API for:
- Data center management
- Kubernetes cluster provisioning
- Network configuration
- Storage provisioning
- Container registry management

## Authentication

The IONOS Cloud API supports two authentication methods:

1. **Basic Authentication** (username + password)
   - **Note**: From March 15, 2024, only accessible if 2FA is NOT enabled
   
2. **Token Authentication** (Bearer token)
   - Recommended for production use
   - Set as `IONOS_ADMIN_TOKEN` secret in GitHub Actions

## Related Files

- Terraform modules: `terraform/modules/`
- Infrastructure configurations: `terraform/environments/`
- Deployment workflows: `.github/workflows/`

## Version

- API Version: 6.0
- SDK Patch Level: 5
- OpenAPI Version: 3.0.3
