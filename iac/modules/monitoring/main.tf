resource "azurerm_log_analytics_workspace" "lab" {
  count               = var.create ? 1 : 0
  name                = "${var.name_prefix}-logs"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = var.tags
}
data "azurerm_log_analytics_workspace" "existing" {
  count               = var.create ? 0 : 1
  name                = basename(var.existing_log_analytics_id)
  resource_group_name = split("/", var.existing_log_analytics_id)[4]
}
resource "azurerm_application_insights" "lab" {
  count               = var.create ? 1 : 0
  name                = "${var.name_prefix}-insights"
  location            = var.location
  resource_group_name = var.resource_group_name
  workspace_id        = azurerm_log_analytics_workspace.lab[0].id
  application_type    = "web"
  tags                = var.tags
}
data "azurerm_application_insights" "existing" {
  count               = var.create ? 0 : 1
  name                = basename(var.existing_app_insights_id)
  resource_group_name = split("/", var.existing_app_insights_id)[4]
}
