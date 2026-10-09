variable "create" {
  description = "Create this platform; false uses supplied existing resource."
  type        = bool
}
variable "existing_id" {
  description = "Existing platform ARM resource ID."
  type        = string
}
variable "name_prefix" {
  description = "Resource name prefix."
  type        = string
}
variable "resource_group_name" {
  description = "Resource group for created resources."
  type        = string
}
variable "location" {
  description = "Azure deployment region."
  type        = string
}
variable "tags" {
  description = "Resource tags."
  type        = map(string)
}
variable "tenant_id" {
  description = "Entra directory ID."
  type        = string
}
variable "deployer_object_id" {
  description = "Identity allowed to manage secrets in a created vault."
  type        = string
}
variable "create_private_endpoint" {
  description = "Create Key Vault private endpoint."
  type        = bool
}
variable "create_private_dns_zone" {
  description = "Create private DNS zone instead of reusing existing zone."
  type        = bool
}
variable "existing_private_dns_zone_id" {
  description = "Existing privatelink.vaultcore.azure.net DNS zone ARM ID."
  type        = string
}
variable "vnet_id" {
  description = "Virtual network linked to Key Vault private DNS."
  type        = string
}
variable "endpoint_subnet_id" {
  description = "Private endpoint subnet ARM ID."
  type        = string
}
