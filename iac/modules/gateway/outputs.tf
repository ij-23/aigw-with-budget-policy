output "id" { value = local.service.id }
output "name" { value = local.service.name }
output "resource_group_name" { value = local.service.resource_group_name }
output "url" { value = local.service.gateway_url }
output "principal_id" { value = local.service.identity[0].principal_id }
