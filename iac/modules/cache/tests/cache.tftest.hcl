mock_provider "azurerm" {
  mock_resource "azurerm_private_dns_zone" {
    defaults = {
      id = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-test-budget/providers/Microsoft.Network/privateDnsZones/privatelink.redis.azure.net"
    }
  }
}

variables {
  create                       = true
  name_prefix                  = "test-budget"
  resource_group_name          = "rg-test-budget"
  location                     = "eastus2"
  cache_location               = "eastus2"
  tags                         = {}
  existing_connection_string   = ""
  sku_name                     = "Balanced_B0"
  high_availability_enabled    = false
  vnet_id                      = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-test-budget/providers/Microsoft.Network/virtualNetworks/test"
  endpoint_subnet_id           = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-test-budget/providers/Microsoft.Network/virtualNetworks/test/subnets/endpoints"
  create_private_dns_zone      = true
  existing_private_dns_zone_id = ""
  log_analytics_id             = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-test-budget/providers/Microsoft.OperationalInsights/workspaces/test"
}

run "managed_cache_is_private_and_keyless" {
  command = plan
  assert {
    condition     = azurerm_managed_redis.lab[0].public_network_access == "Disabled" && azurerm_managed_redis.lab[0].default_database[0].client_protocol == "Encrypted" && !azurerm_managed_redis.lab[0].default_database[0].access_keys_authentication_enabled
    error_message = "Managed Redis must require private TLS connections and Entra authentication."
  }
  assert {
    condition     = azurerm_managed_redis.lab[0].default_database[0].clustering_policy == "NoCluster" && azurerm_managed_redis.lab[0].default_database[0].eviction_policy == "NoEviction"
    error_message = "Maintain application compatibility and do not evict active counters."
  }
}

run "existing_cache_creates_no_resources" {
  command = plan
  variables {
    create                     = false
    existing_connection_string = "existing.eastus2.redis.azure.net:10000,ssl=True,abortConnect=False"
  }
  assert {
    condition     = length(azurerm_managed_redis.lab) == 0 && length(azurerm_private_endpoint.redis) == 0 && length(azurerm_private_dns_zone.redis) == 0 && length(azurerm_private_dns_zone_virtual_network_link.redis) == 0 && length(azurerm_monitor_diagnostic_setting.metrics) == 0 && length(azurerm_monitor_diagnostic_setting.connections) == 0
    error_message = "Disabling creation must not provision any Redis resources."
  }
  assert {
    condition     = output.connection_string == var.existing_connection_string && output.id == null
    error_message = "Reuse the supplied cache connection."
  }
}

run "missing_existing_cache_is_rejected" {
  command = plan
  variables {
    create = false
  }
  expect_failures = [var.existing_connection_string]
}
