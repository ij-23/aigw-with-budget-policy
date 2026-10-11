variable "create" {
  description = "Create Azure Managed Redis; false creates no cache resources and requires an existing connection."
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
  description = "Azure region for Managed Redis."
  type        = string
}
variable "tags" {
  description = "Resource tags."
  type        = map(string)
}
variable "existing_connection_string" {
  description = "Existing reachable Redis connection string; engine still requires Redis when creation is disabled."
  type        = string
  sensitive   = true
  validation {
    condition     = var.create || length(trimspace(var.existing_connection_string)) > 0
    error_message = "When create=false, supply existing_connection_string. This engine does not support running without Redis."
  }
}
variable "sku_name" {
  description = "Managed Redis SKU supporting NoCluster (up to 25 GB)."
  type        = string
}
variable "high_availability_enabled" {
  description = "Enable Managed Redis high availability; false reduces POC infrastructure cost."
  type        = bool
}
variable "vnet_id" {
  description = "VNet ARM ID for private DNS resolution."
  type        = string
}
variable "endpoint_subnet_id" {
  description = "Private endpoint subnet ARM ID."
  type        = string
}
variable "create_private_dns_zone" {
  description = "Create the Redis private DNS zone; false reuses an existing zone."
  type        = bool
}
variable "existing_private_dns_zone_id" {
  description = "Existing privatelink.redis.azure.net zone ARM ID."
  type        = string
  validation {
    condition     = !var.create || var.create_private_dns_zone || length(trimspace(var.existing_private_dns_zone_id)) > 0
    error_message = "Supply existing_private_dns_zone_id when creating Redis without creating its DNS zone."
  }
}

variable "log_analytics_id" {
  description = "Log Analytics workspace ARM ID for Redis metrics and connection events."
  type        = string
}

variable "cache_location" {
  description = "Managed Redis region; private endpoint remains in the VNet deployment region."
  type        = string
}
