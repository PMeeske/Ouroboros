output "datacenter_id" {
  description = "ID of the data center"
  value       = ionoscloud_datacenter.main.id
}

output "datacenter_name" {
  description = "Name of the data center"
  value       = ionoscloud_datacenter.main.name
}

output "datacenter_location" {
  description = "Location of the data center"
  value       = ionoscloud_datacenter.main.location
}
