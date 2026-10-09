variable "create" {
  description = "Create this platform; false uses supplied existing resource."
  type        = bool
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
variable "existing_id" {
  description = "Existing platform ARM resource ID."
  type        = string
}
variable "log_analytics_id" {
  description = "Log Analytics workspace ARM resource ID."
  type        = string
}
variable "create_project" {
  description = "Create a project in the selected Foundry account."
  type        = bool
}
variable "create_deployment" {
  description = "Create a model deployment in the selected account."
  type        = bool
}
variable "deployment_name" {
  description = "Deployment name."
  type        = string
}
variable "model_name" {
  description = "Model name."
  type        = string
}
variable "model_version" {
  description = "Pinned model version."
  type        = string
}
variable "model_sku" {
  description = "Model deployment SKU."
  type        = string
}
variable "model_capacity" {
  description = "Model capacity in quota units."
  type        = number
}
