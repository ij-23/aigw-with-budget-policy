output "registry_id" { value = var.create_registry ? azurerm_container_registry.lab[0].id : data.azurerm_container_registry.existing[0].id }
output "registry_name" { value = var.create_registry ? azurerm_container_registry.lab[0].name : data.azurerm_container_registry.existing[0].name }
output "registry_server" { value = var.create_registry ? azurerm_container_registry.lab[0].login_server : data.azurerm_container_registry.existing[0].login_server }
output "environment_id" { value = var.create_environment ? azurerm_container_app_environment.lab[0].id : var.existing_environment_id }
