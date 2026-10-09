resource "azurerm_key_vault" "lab" {
  count                         = var.create ? 1 : 0
  name                          = "${var.name_prefix}-kv"
  resource_group_name           = var.resource_group_name
  location                      = var.location
  tenant_id                     = var.tenant_id
  sku_name                      = "standard"
  rbac_authorization_enabled    = true
  public_network_access_enabled = false
  soft_delete_retention_days    = 7
  tags                          = var.tags
}
locals { id = var.create ? azurerm_key_vault.lab[0].id : var.existing_id }
resource "azurerm_role_assignment" "deployer" {
  count                = var.create ? 1 : 0
  scope                = local.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = var.deployer_object_id
}

resource "azurerm_private_dns_zone" "vault" {
  count               = var.create_private_endpoint && var.create_private_dns_zone ? 1 : 0
  name                = "privatelink.vaultcore.azure.net"
  resource_group_name = var.resource_group_name
  tags                = var.tags
}
locals {
  private_dns_zone_id = var.create_private_dns_zone ? try(azurerm_private_dns_zone.vault[0].id, "") : var.existing_private_dns_zone_id
}
resource "azurerm_private_dns_zone_virtual_network_link" "vault" {
  count                 = var.create_private_endpoint ? 1 : 0
  name                  = "${var.name_prefix}-vault"
  resource_group_name   = split("/", local.private_dns_zone_id)[4]
  private_dns_zone_name = basename(local.private_dns_zone_id)
  virtual_network_id    = var.vnet_id
  registration_enabled  = false
  tags                  = var.tags
}
resource "azurerm_private_endpoint" "vault" {
  count               = var.create_private_endpoint ? 1 : 0
  name                = "${var.name_prefix}-vault-pe"
  resource_group_name = var.resource_group_name
  location            = var.location
  subnet_id           = var.endpoint_subnet_id
  tags                = var.tags
  private_service_connection {
    name                           = "vault"
    private_connection_resource_id = local.id
    subresource_names              = ["vault"]
    is_manual_connection           = false
  }
  private_dns_zone_group {
    name                 = "vault"
    private_dns_zone_ids = [local.private_dns_zone_id]
  }
}
