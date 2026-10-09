output "api_application_id" { value = azuread_application.api.id }
output "api_client_id" { value = azuread_application.api.client_id }
output "gateway_client_id" { value = azuread_application.gateway.client_id }
output "test_client_id" { value = azuread_application.test.client_id }
output "admin_client_id" { value = azuread_application.admin.client_id }
output "admin_client_secret" {
  value     = azuread_application_password.admin.value
  sensitive = true
}
