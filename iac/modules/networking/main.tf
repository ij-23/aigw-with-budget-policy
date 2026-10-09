resource "azurerm_virtual_network" "lab" {
  count               = var.create ? 1 : 0
  name                = "${var.name_prefix}-vnet"
  resource_group_name = var.resource_group_name
  location            = var.location
  address_space       = [var.address_space]
  tags                = var.tags
}
resource "azurerm_subnet" "apps" {
  count                = var.create ? 1 : 0
  name                 = "container-apps"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.lab[0].name
  address_prefixes     = [var.apps_subnet_cidr]
  delegation {
    name = "container-apps"
    service_delegation {
      name    = "Microsoft.App/environments"
      actions = ["Microsoft.Network/virtualNetworks/subnets/join/action"]
    }
  }
}
resource "azurerm_subnet" "endpoints" {
  count                             = var.create ? 1 : 0
  name                              = "private-endpoints"
  resource_group_name               = var.resource_group_name
  virtual_network_name              = azurerm_virtual_network.lab[0].name
  address_prefixes                  = [var.endpoints_subnet_cidr]
  private_endpoint_network_policies = "Disabled"
}
