output "id" {
  description = "Created Managed Redis ARM ID, or null when cache creation is disabled."
  value       = var.create ? azurerm_managed_redis.lab[0].id : null
}
output "hostname" {
  description = "Created Managed Redis TLS hostname, or null when cache creation is disabled."
  value       = var.create ? azurerm_managed_redis.lab[0].hostname : null
}
output "connection_string" {
  value      = var.create ? "${azurerm_managed_redis.lab[0].hostname}:${azurerm_managed_redis.lab[0].default_database[0].port},ssl=True,abortConnect=False" : var.existing_connection_string
  sensitive  = true
  depends_on = [azurerm_private_endpoint.redis, azurerm_private_dns_zone_virtual_network_link.redis]
}
