variable "create" {
  description = "Create PostgreSQL Flexible Server and aipolicy database; false uses supplied existing server and connection string."
  type        = bool
}
variable "existing_id" {
  description = "Existing PostgreSQL Flexible Server ARM ID."
  type        = string
  default     = ""
  validation {
    condition     = var.create || trimspace(var.existing_id) != ""
    error_message = "existing_id is required when create=false."
  }
}
variable "existing_connection_string" {
  description = "Existing Npgsql connection string with TLS. Runtime identity needs DDL and CRUD privileges in the selected database."
  type        = string
  sensitive   = true
  default     = ""
  validation {
    condition     = var.create || trimspace(var.existing_connection_string) != ""
    error_message = "existing_connection_string is required when create=false."
  }
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
variable "sku_name" {
  description = "PostgreSQL compute SKU; B_Standard_B1ms is the small burstable POC default."
  type        = string
  default     = "B_Standard_B1ms"
}
variable "storage_mb" {
  description = "PostgreSQL storage allocation in MiB."
  type        = number
  default     = 32768
}
variable "log_analytics_id" {
  description = "Log Analytics workspace ARM ID."
  type        = string
}
variable "create_private_endpoint" {
  description = "Create a private endpoint; existing servers may reuse their own private connectivity."
  type        = bool
}
variable "create_private_dns_zone" {
  description = "Create private DNS zone instead of reusing an existing zone."
  type        = bool
}
variable "existing_private_dns_zone_id" {
  description = "Existing privatelink.postgres.database.azure.com DNS zone ARM ID."
  type        = string
}
variable "vnet_id" {
  description = "Virtual network linked to PostgreSQL private DNS."
  type        = string
}
variable "endpoint_subnet_id" {
  description = "Private endpoint subnet ARM ID."
  type        = string
}
