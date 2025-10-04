output "registry_id" {
  description = "ID of the container registry"
  value       = ionoscloud_container_registry.main.id
}

output "registry_name" {
  description = "Name of the container registry"
  value       = ionoscloud_container_registry.main.name
}

output "registry_hostname" {
  description = "Hostname of the container registry"
  value       = ionoscloud_container_registry.main.hostname
}

output "registry_location" {
  description = "Location of the container registry"
  value       = ionoscloud_container_registry.main.location
}

output "registry_token_id" {
  description = "ID of the registry token"
  value       = ionoscloud_container_registry_token.main.id
}

output "registry_token_credentials" {
  description = "Registry token credentials"
  value       = ionoscloud_container_registry_token.main.credentials
  sensitive   = true
}
