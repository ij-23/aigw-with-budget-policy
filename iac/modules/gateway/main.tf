resource "azurerm_api_management" "lab" {
  count               = var.create ? 1 : 0
  name                = "${var.name_prefix}-apim"
  resource_group_name = var.resource_group_name
  location            = var.location
  publisher_email     = var.publisher_email
  publisher_name      = "AI Budget Lab"
  sku_name            = var.sku
  identity { type = "SystemAssigned" }
  tags = var.tags
}
data "azurerm_api_management" "existing" {
  count               = var.create ? 0 : 1
  name                = basename(var.existing_id)
  resource_group_name = split("/", var.existing_id)[4]
}
locals { service = var.create ? azurerm_api_management.lab[0] : data.azurerm_api_management.existing[0] }
