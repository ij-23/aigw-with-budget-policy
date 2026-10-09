resource "azurerm_container_registry" "lab" {
  count               = var.create_registry ? 1 : 0
  name                = "${var.name_prefix}acr"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "Basic"
  admin_enabled       = false
  tags                = var.tags
}
data "azurerm_container_registry" "existing" {
  count               = var.create_registry ? 0 : 1
  name                = basename(var.existing_registry_id)
  resource_group_name = split("/", var.existing_registry_id)[4]
}
resource "azurerm_container_app_environment" "lab" {
  count                      = var.create_environment ? 1 : 0
  name                       = "${var.name_prefix}-vnet-env"
  location                   = var.location
  resource_group_name        = var.resource_group_name
  log_analytics_workspace_id = var.log_analytics_id
  infrastructure_subnet_id   = var.apps_subnet_id
  workload_profile {
    name                  = "Consumption"
    workload_profile_type = "Consumption"
  }
  tags = var.tags
}
