resource "azurerm_cosmosdb_account" "lab" {
  count                            = var.create ? 1 : 0
  name                             = "${var.name_prefix}-cosmos"
  location                         = var.location
  resource_group_name              = var.resource_group_name
  offer_type                       = "Standard"
  kind                             = "GlobalDocumentDB"
  public_network_access_enabled    = false
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
resource "azurerm_private_dns_zone" "cosmos" {
  count               = var.create_private_endpoint && var.create_private_dns_zone ? 1 : 0
  name                = "privatelink.documents.azure.com"
  resource_group_name = var.resource_group_name
  tags                = var.tags
}
locals {
  private_dns_zone_id = var.create_private_dns_zone ? try(azurerm_private_dns_zone.cosmos[0].id, "") : var.existing_private_dns_zone_id
}
resource "azurerm_private_dns_zone_virtual_network_link" "cosmos" {
  count                 = var.create_private_endpoint ? 1 : 0
  name                  = "${var.name_prefix}-cosmos"
  resource_group_name   = split("/", local.private_dns_zone_id)[4]
  private_dns_zone_name = basename(local.private_dns_zone_id)
  virtual_network_id    = var.vnet_id
  registration_enabled  = false
  tags                  = var.tags
}
resource "azurerm_private_endpoint" "cosmos" {
  count               = var.create_private_endpoint ? 1 : 0
  name                = "${var.name_prefix}-cosmos-pe"
  resource_group_name = var.resource_group_name
  location            = var.location
  subnet_id           = var.endpoint_subnet_id
  tags                = var.tags
  private_service_connection {
    name                           = "cosmos-sql"
    private_connection_resource_id = local.account.id
    subresource_names              = ["Sql"]
    is_manual_connection           = false
  }
  private_dns_zone_group {
    name                 = "cosmos"
    private_dns_zone_ids = [local.private_dns_zone_id]
  }
}
