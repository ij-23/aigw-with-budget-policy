resource "azurerm_key_vault" "lab" {
  count                      = var.create ? 1 : 0
  name                       = "${var.name_prefix}-kv"
  resource_group_name        = var.resource_group_name
  location                   = var.location
  tenant_id                  = var.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true
  soft_delete_retention_days = 7
  tags                       = var.tags
}
locals { id = var.create ? azurerm_key_vault.lab[0].id : var.existing_id }
resource "azurerm_role_assignment" "deployer" {
  count                = var.create ? 1 : 0
  scope                = local.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = var.deployer_object_id
}
