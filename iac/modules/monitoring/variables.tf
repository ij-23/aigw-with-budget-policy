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
variable "existing_log_analytics_id" {
  description = "Existing workspace ARM ID."
  type        = string
}
variable "existing_app_insights_id" {
  description = "Existing App Insights ARM ID."
  type        = string
}
