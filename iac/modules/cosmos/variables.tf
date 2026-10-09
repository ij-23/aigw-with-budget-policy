variable "create" {
  description = "Create this platform; false uses supplied existing resource."
  type        = bool
}
variable "create_schema" {
  description = "Create aipolicy database and containers; false assumes an existing schema."
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
