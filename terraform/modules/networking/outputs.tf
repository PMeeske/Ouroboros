output "lan_id" {
  description = "ID of the LAN"
  value       = ionoscloud_lan.main.id
}

output "lan_name" {
  description = "Name of the LAN"
  value       = ionoscloud_lan.main.name
}

output "lan_public" {
  description = "Whether the LAN is public"
  value       = ionoscloud_lan.main.public
}
