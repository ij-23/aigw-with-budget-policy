resource "azurerm_cosmosdb_account" "lab" {
  count                            = var.create ? 1 : 0
  name                             = "${var.name_prefix}-cosmos"
  location                         = var.location
  resource_group_name              = var.resource_group_name
  offer_type                       = "Standard"
  kind                             = "GlobalDocumentDB"
  local_authentication_enabled     = false
  multiple_write_locations_enabled = false
  consistency_policy { consistency_level = "Session" }
  capabilities { name = "EnableServerless" }
  geo_location {
    location          = var.location
    failover_priority = 0
  }
  tags = var.tags
}
data "azurerm_cosmosdb_account" "existing" {
  count               = var.create ? 0 : 1
  name                = basename(var.existing_id)
  resource_group_name = split("/", var.existing_id)[4]
  lifecycle {
    postcondition {
      condition     = !self.multiple_write_locations_enabled
      error_message = "Hard-budget enforcement requires one Cosmos write region."
    }
  }
}
locals {
  account = var.create ? azurerm_cosmosdb_account.lab[0] : data.azurerm_cosmosdb_account.existing[0]
}
resource "azurerm_cosmosdb_sql_database" "lab" {
  count               = var.create_schema ? 1 : 0
  name                = "aipolicy"
  resource_group_name = local.account.resource_group_name
  account_name        = local.account.name
}
resource "azurerm_cosmosdb_sql_container" "lab" {
  for_each            = var.create_schema ? { configuration = "/partitionKey", "audit-logs" = "/customerKey", "billing-summaries" = "/customerKey" } : {}
  name                = each.key
  resource_group_name = local.account.resource_group_name
  account_name        = local.account.name
  database_name       = azurerm_cosmosdb_sql_database.lab[0].name
  partition_key_paths = [each.value]
  default_ttl         = -1
}
moved {
  from = azurerm_cosmosdb_sql_database.lab
  to   = azurerm_cosmosdb_sql_database.lab[0]
}
resource "azurerm_monitor_diagnostic_setting" "lab" {
  count                      = var.create ? 1 : 0
  name                       = "lab-diagnostics"
  target_resource_id         = local.account.id
  log_analytics_workspace_id = var.log_analytics_id
  enabled_log { category = "DataPlaneRequests" }
  enabled_metric { category = "Requests" }
}
