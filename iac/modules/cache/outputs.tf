output "connection_string" {
  value     = var.create ? "${azurerm_container_app.redis[0].name}:6379,password=${random_password.redis[0].result},ssl=False,abortConnect=False" : var.existing_connection_string
  sensitive = true
}
