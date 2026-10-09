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
