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
variable "environment_id" {
  description = "Container Apps environment ARM ID."
  type        = string
}
variable "registry_id" {
  description = "ACR ARM ID."
  type        = string
}
variable "registry_server" {
  description = "ACR login hostname."
  type        = string
}
variable "container_image" {
  description = "Container image or empty for bootstrap."
  type        = string
}
variable "redis_connection_string" {
  description = "Redis connection string."
  type        = string
  sensitive   = true
}
variable "cosmos_endpoint" {
  description = "Cosmos HTTPS endpoint."
  type        = string
}
variable "app_insights_connection_string" {
  description = "Application Insights connection string."
  type        = string
  sensitive   = true
}
variable "tenant_id" {
  description = "Entra directory ID."
  type        = string
}
variable "api_client_id" {
  description = "Backend API application client ID."
  type        = string
}
variable "subscription_id" {
  description = "Azure subscription ID."
  type        = string
}
variable "apim_id" {
  description = "APIM ARM ID."
  type        = string
}
variable "foundry_id" {
  description = "Foundry account ARM ID, used for discovery with account-scoped Reader permission."
  type        = string
}
