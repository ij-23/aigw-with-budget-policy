mock_provider "azurerm" {
  mock_resource "azurerm_private_dns_zone" {
    defaults = { id = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-test/providers/Microsoft.Network/privateDnsZones/privatelink.postgres.database.azure.com" }
  }
}
mock_provider "random" {}
variables {
  create                       = true
  name_prefix                  = "budgettest"
  resource_group_name          = "rg-test"
  location                     = "swedencentral"
  tags                         = {}
  log_analytics_id             = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-test/providers/Microsoft.OperationalInsights/workspaces/test"
  create_private_endpoint      = true
  create_private_dns_zone      = true
  existing_private_dns_zone_id = ""
  vnet_id                      = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-test/providers/Microsoft.Network/virtualNetworks/test"
  endpoint_subnet_id           = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-test/providers/Microsoft.Network/virtualNetworks/test/subnets/endpoints"
}
run "database_is_private_and_small" {
  command = plan
  assert {
    condition     = !azurerm_postgresql_flexible_server.lab[0].public_network_access_enabled && azurerm_postgresql_flexible_server.lab[0].sku_name == "B_Standard_B1ms" && azurerm_postgresql_flexible_server.lab[0].location == "swedencentral"
    error_message = "POC database must use private networking and the small Sweden Central default."
  }
}
run "reuse_does_not_create_server_or_database" {
  command = plan
  variables {
    create                     = false
    existing_id                = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-test/providers/Microsoft.DBforPostgreSQL/flexibleServers/existing"
    existing_connection_string = "Host=existing.postgres.database.azure.com;Database=aipolicy;Username=app;Password=example;SSL Mode=VerifyFull"
    create_private_endpoint    = false
  }
  assert {
    condition     = length(azurerm_postgresql_flexible_server.lab) == 0 && length(azurerm_postgresql_flexible_server_database.lab) == 0 && length(azurerm_private_endpoint.postgres) == 0 && output.connection_string == var.existing_connection_string
    error_message = "Existing server reuse must create no database platform resources and preserve the connection."
  }
}
run "missing_existing_connection_is_rejected" {
  command = plan
  variables {
    create                  = false
    existing_id             = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-test/providers/Microsoft.DBforPostgreSQL/flexibleServers/existing"
    create_private_endpoint = false
  }
  expect_failures = [var.existing_connection_string]
}
