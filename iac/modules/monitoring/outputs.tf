output "workspace_id" { value = var.create ? azurerm_log_analytics_workspace.lab[0].id : data.azurerm_log_analytics_workspace.existing[0].id }
output "connection_string" {
  value     = var.create ? azurerm_application_insights.lab[0].connection_string : data.azurerm_application_insights.existing[0].connection_string
  sensitive = true
}
