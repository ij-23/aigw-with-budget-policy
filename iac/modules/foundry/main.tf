resource "azapi_resource" "account" {
  count     = var.create ? 1 : 0
  type      = "Microsoft.CognitiveServices/accounts@2025-06-01"
  name      = "${var.name_prefix}-foundry"
  parent_id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/${var.resource_group_name}"
  location  = var.location
  tags      = var.tags
  identity { type = "SystemAssigned" }
  body = {
    kind = "AIServices"
    sku  = { name = "S0" }
    properties = {
      customSubDomainName    = "${var.name_prefix}-foundry"
      allowProjectManagement = true
      disableLocalAuth       = true
      publicNetworkAccess    = "Enabled"
    }
  }
}
data "azurerm_client_config" "current" {}
data "azurerm_cognitive_account" "existing" {
  count               = var.create ? 0 : 1
  name                = basename(var.existing_id)
  resource_group_name = split("/", var.existing_id)[4]
}
locals {
  id       = var.create ? azapi_resource.account[0].id : data.azurerm_cognitive_account.existing[0].id
  endpoint = var.create ? "https://${var.name_prefix}-foundry.openai.azure.com/" : data.azurerm_cognitive_account.existing[0].endpoint
}
resource "azapi_resource" "project" {
  count     = var.create_project ? 1 : 0
  type      = "Microsoft.CognitiveServices/accounts/projects@2025-06-01"
  name      = "budget-lab"
  parent_id = local.id
  location  = var.location
  identity { type = "SystemAssigned" }
  body       = { properties = { displayName = "AI gateway budget lab", description = "APIM $50 monthly budget validation" } }
  depends_on = [azurerm_cognitive_deployment.chat]
}
resource "azurerm_cognitive_deployment" "chat" {
  count                  = var.create_deployment ? 1 : 0
  name                   = var.deployment_name
  cognitive_account_id   = local.id
  version_upgrade_option = "NoAutoUpgrade"
  model {
    format  = "OpenAI"
    name    = var.model_name
    version = var.model_version
  }
  sku {
    name     = var.model_sku
    capacity = var.model_capacity
  }
}
resource "azurerm_monitor_diagnostic_setting" "lab" {
  count                      = var.create ? 1 : 0
  name                       = "lab-diagnostics"
  target_resource_id         = local.id
  log_analytics_workspace_id = var.log_analytics_id
  enabled_log { category = "RequestResponse" }
  enabled_metric { category = "AllMetrics" }
}
