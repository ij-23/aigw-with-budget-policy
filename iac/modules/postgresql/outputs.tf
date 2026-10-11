output "id" { value = local.server_id }
output "name" { value = basename(local.server_id) }
output "hostname" { value = var.create ? azurerm_postgresql_flexible_server.lab[0].fqdn : "" }
output "connection_string" {
  value     = var.create ? "Host=${azurerm_postgresql_flexible_server.lab[0].fqdn};Database=${azurerm_postgresql_flexible_server_database.lab[0].name};Username=budgetadmin;Password=${random_password.admin[0].result};SSL Mode=VerifyFull;Timeout=30;Command Timeout=30" : var.existing_connection_string
  sensitive = true
}
