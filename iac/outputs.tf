output "resource_group_name" { value = local.resource_group_name }
output "subscription_id" { value = var.subscription_id }
output "tenant_id" { value = var.tenant_id }
output "dashboard_url" { value = module.engine.url }
output "policy_engine_name" { value = module.engine.name }
output "policy_engine_id" { value = module.engine.id }
output "registry_name" { value = module.hosting.registry_name }
output "registry_server" { value = module.hosting.registry_server }
output "apim_resource_id" { value = module.gateway.id }
output "apim_gateway_url" { value = module.gateway.url }
output "apim_chat_url" { value = "${module.gateway.url}/budget/openai/deployments/${var.deployment_name}/chat/completions?api-version=2024-10-21" }
output "api_client_id" { value = local.api_client_id }
output "gateway_client_id" { value = local.gateway_client_id }
output "test_client_id" { value = local.test_client_id }
output "admin_client_id" { value = local.admin_client_id }
output "admin_client_secret" {
  value     = local.admin_client_secret
  sensitive = true
}
output "test_user_object_id" { value = var.create_test_user ? azuread_user.test[0].object_id : var.existing_test_user_object_id }
output "test_user_upn" { value = var.create_test_user ? azuread_user.test[0].user_principal_name : "" }
output "test_user_password" {
  value     = var.create_test_user ? random_password.test_user[0].result : ""
  sensitive = true
}
output "deployment_name" { value = var.deployment_name }
output "foundry_id" { value = module.foundry.id }
output "foundry_endpoint" { value = module.foundry.endpoint }
output "key_vault_name" { value = module.vault.name }
output "cosmos_id" { value = module.cosmos.id }
output "rendered_policy" { value = local.rendered_policy }
