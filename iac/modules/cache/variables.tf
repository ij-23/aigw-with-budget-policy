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
variable "tags" {
  description = "Resource tags."
  type        = map(string)
}
variable "environment_id" {
  description = "Container Apps environment ARM ID."
  type        = string
}
variable "existing_connection_string" {
  description = "Existing Redis connection string."
  type        = string
  sensitive   = true
}
