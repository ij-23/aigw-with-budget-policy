resource "random_password" "admin" {
  count   = var.create ? 1 : 0
  length  = 32
  special = false
}
resource "azurerm_postgresql_flexible_server" "lab" {
  count                         = var.create ? 1 : 0
  name                          = "${var.name_prefix}-postgres"
  resource_group_name           = var.resource_group_name
  location                      = var.location
  version                       = "17"
  administrator_login           = "budgetadmin"
  administrator_password        = random_password.admin[0].result
  sku_name                      = var.sku_name
  storage_mb                    = var.storage_mb
  backup_retention_days         = 7
  public_network_access_enabled = false
  tags                          = var.tags
  # Azure chooses the available zone when none is requested. Preserve that placement.
  lifecycle { ignore_changes = [zone] }
}
resource "azurerm_postgresql_flexible_server_database" "lab" {
  count     = var.create ? 1 : 0
  name      = "aipolicy"
  server_id = azurerm_postgresql_flexible_server.lab[0].id
  charset   = "UTF8"
  collation = "en_US.utf8"
}
locals {
  server_id = var.create ? azurerm_postgresql_flexible_server.lab[0].id : var.existing_id
}
resource "azurerm_private_dns_zone" "postgres" {
  count               = var.create_private_endpoint && var.create_private_dns_zone ? 1 : 0
  name                = "privatelink.postgres.database.azure.com"
  resource_group_name = var.resource_group_name
  tags                = var.tags
}
locals {
  private_dns_zone_id = var.create_private_dns_zone ? try(azurerm_private_dns_zone.postgres[0].id, "") : var.existing_private_dns_zone_id
}
resource "azurerm_private_dns_zone_virtual_network_link" "postgres" {
  count                 = var.create_private_endpoint ? 1 : 0
  name                  = "${var.name_prefix}-postgres"
  resource_group_name   = split("/", local.private_dns_zone_id)[4]
  private_dns_zone_name = basename(local.private_dns_zone_id)
  virtual_network_id    = var.vnet_id
  registration_enabled  = false
  tags                  = var.tags
}
resource "azurerm_private_endpoint" "postgres" {
  count               = var.create_private_endpoint ? 1 : 0
  name                = "${var.name_prefix}-postgres-pe"
  resource_group_name = var.resource_group_name
  location            = var.location
  subnet_id           = var.endpoint_subnet_id
  tags                = var.tags
  private_service_connection {
    name                           = "postgresql"
    private_connection_resource_id = local.server_id
    subresource_names              = ["postgresqlServer"]
    is_manual_connection           = false
  }
  private_dns_zone_group {
    name                 = "postgresql"
    private_dns_zone_ids = [local.private_dns_zone_id]
  }
}
resource "azurerm_monitor_diagnostic_setting" "lab" {
  count                      = var.create ? 1 : 0
  name                       = "lab-diagnostics"
  target_resource_id         = local.server_id
  log_analytics_workspace_id = var.log_analytics_id
  enabled_log { category = "PostgreSQLLogs" }
  enabled_metric { category = "AllMetrics" }
}
