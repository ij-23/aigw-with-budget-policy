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
variable "log_analytics_id" {
  description = "Log Analytics workspace ARM resource ID."
  type        = string
}
variable "create_registry" {
  description = "Create ACR instead of using existing_registry_id."
  type        = bool
}
variable "create_environment" {
  description = "Create Container Apps environment instead of using existing_environment_id."
  type        = bool
}
variable "existing_registry_id" {
  description = "Existing ACR ARM ID."
  type        = string
}
variable "existing_environment_id" {
  description = "Existing Container Apps environment ARM ID."
  type        = string
}
variable "apps_subnet_id" {
  description = "Dedicated delegated subnet for VNet-connected Container Apps environment."
  type        = string
}
