locals { app = var.create ? azurerm_container_app.engine[0] : data.azurerm_container_app.existing[0] }
output "id" { value = local.app.id }
output "name" { value = local.app.name }
output "url" { value = "https://${local.app.ingress[0].fqdn}" }
output "principal_id" { value = var.create ? azurerm_user_assigned_identity.engine[0].principal_id : (local.app.identity[0].type == "UserAssigned" ? data.azurerm_user_assigned_identity.existing[0].principal_id : local.app.identity[0].principal_id) }
