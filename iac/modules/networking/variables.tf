variable "create" {
  description = "Create a virtual network and dedicated subnets; false uses supplied existing IDs."
  type        = bool
}
variable "name_prefix" {
  description = "Resource name prefix."
  type        = string
}
variable "resource_group_name" {
  description = "Resource group for created networking."
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
variable "address_space" {
  description = "Virtual network CIDR."
  type        = string
}
variable "apps_subnet_cidr" {
  description = "Dedicated Container Apps subnet CIDR."
  type        = string
}
variable "endpoints_subnet_cidr" {
  description = "Private endpoint subnet CIDR."
  type        = string
}
variable "existing_vnet_id" {
  description = "Existing virtual network ARM ID."
  type        = string
}
variable "existing_apps_subnet_id" {
  description = "Existing subnet delegated to Microsoft.App/environments."
  type        = string
}
variable "existing_endpoints_subnet_id" {
  description = "Existing private endpoint subnet ARM ID."
  type        = string
}
