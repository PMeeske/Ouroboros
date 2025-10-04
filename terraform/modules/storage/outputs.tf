output "volume_ids" {
  description = "Map of volume names to IDs"
  value       = { for k, v in ionoscloud_volume.volumes : k => v.id }
}

output "volumes" {
  description = "Map of volume details"
  value = { for k, v in ionoscloud_volume.volumes : k => {
    id   = v.id
    name = v.name
    size = v.size
    type = v.type
  } }
}
