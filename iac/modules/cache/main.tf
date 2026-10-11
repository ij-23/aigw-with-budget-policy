resource "azurerm_managed_redis" "lab" {
  count                     = var.create ? 1 : 0
  name                      = "${var.name_prefix}-redis"
  resource_group_name       = var.resource_group_name
  location                  = var.cache_location
  sku_name                  = var.sku_name
  high_availability_enabled = var.high_availability_enabled
  public_network_access     = "Disabled"
  tags                      = var.tags
  default_database {
    access_keys_authentication_enabled = false
    client_protocol                    = "Encrypted"
    clustering_policy                  = "NoCluster"
    eviction_policy                    = "NoEviction"
  }
}
resource "azurerm_private_dns_zone" "redis" {
  count               = var.create && var.create_private_dns_zone ? 1 : 0
  name                = "privatelink.redis.azure.net"
  resource_group_name = var.resource_group_name
  tags                = var.tags
}
locals {
  private_dns_zone_id = var.create_private_dns_zone ? try(azurerm_private_dns_zone.redis[0].id, "") : var.existing_private_dns_zone_id
}
resource "azurerm_private_dns_zone_virtual_network_link" "redis" {
  count                 = var.create ? 1 : 0
  name                  = "${var.name_prefix}-redis"
  resource_group_name   = split("/", local.private_dns_zone_id)[4]
  private_dns_zone_name = basename(local.private_dns_zone_id)
  virtual_network_id    = var.vnet_id
  registration_enabled  = false
  tags                  = var.tags
}
resource "azurerm_private_endpoint" "redis" {
  count               = var.create ? 1 : 0
  name                = "${var.name_prefix}-redis-pe"
  resource_group_name = var.resource_group_name
  location            = var.location
  subnet_id           = var.endpoint_subnet_id
  tags                = var.tags
  private_service_connection {
    name                           = "redis"
    private_connection_resource_id = azurerm_managed_redis.lab[0].id
    subresource_names              = ["redisEnterprise"]
    is_manual_connection           = false
  }
  private_dns_zone_group {
    name                 = "redis"
    private_dns_zone_ids = [local.private_dns_zone_id]
  }
}

resource "azurerm_monitor_diagnostic_setting" "metrics" {
  count                      = var.create ? 1 : 0
  name                       = "lab-metrics"
  target_resource_id         = azurerm_managed_redis.lab[0].id
  log_analytics_workspace_id = var.log_analytics_id
  enabled_metric { category = "AllMetrics" }
}
resource "azurerm_monitor_diagnostic_setting" "connections" {
  count                      = var.create ? 1 : 0
  name                       = "lab-connections"
  target_resource_id         = azurerm_managed_redis.lab[0].default_database[0].id
  log_analytics_workspace_id = var.log_analytics_id
  enabled_log { category = "ConnectionEvents" }
}
