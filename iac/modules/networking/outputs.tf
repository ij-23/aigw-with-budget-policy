output "vnet_id" { value = var.create ? azurerm_virtual_network.lab[0].id : var.existing_vnet_id }
output "apps_subnet_id" { value = var.create ? azurerm_subnet.apps[0].id : var.existing_apps_subnet_id }
output "endpoints_subnet_id" { value = var.create ? azurerm_subnet.endpoints[0].id : var.existing_endpoints_subnet_id }
