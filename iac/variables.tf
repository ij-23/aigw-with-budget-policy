variable "subscription_id" {
  description = "Azure subscription to deploy the lab into."
  type        = string
}
variable "tenant_id" {
  description = "Entra directory for applications and test users."
  type        = string
}
variable "location" {
  description = "Region for the lab services."
  type        = string
  default     = "eastus2"
}
variable "name_prefix" {
  description = "Globally unique lower-case prefix, 3-18 alphanumeric characters."
  type        = string
  default     = "aigwbudgetlab"
  validation {
    condition     = can(regex("^[a-z][a-z0-9]{2,17}$", var.name_prefix))
    error_message = "Use 3-18 lower-case alphanumeric characters, starting with a letter."
  }
}
variable "tags" {
  description = "Tags applied to created resources."
  type        = map(string)
  default     = { project = "aigw-with-budget-policy", environment = "test" }
}
variable "create_resource_group" {
  description = "Create the resource group; false uses resource_group_name."
  type        = bool
  default     = true
}
variable "resource_group_name" {
  description = "Resource group name, also used when bringing an existing group."
  type        = string
  default     = "rg-aigw-budget-lab"
}
variable "create_monitoring" {
  description = "Create Log Analytics and Application Insights; false requires existing IDs."
  type        = bool
  default     = true
}
variable "existing_log_analytics_id" {
  description = "Existing workspace ARM ID when create_monitoring=false."
  type        = string
  default     = ""
}
variable "existing_app_insights_id" {
  description = "Existing App Insights ARM ID when create_monitoring=false."
  type        = string
  default     = ""
}
variable "create_registry" {
  description = "Create ACR; false requires existing_registry_id."
  type        = bool
  default     = true
}
variable "existing_registry_id" {
  description = "Existing ACR ARM ID; the deployer needs image push permission."
  type        = string
  default     = ""
}
variable "create_container_environment" {
  description = "Create a Container Apps environment; false uses the existing environment ID."
  type        = bool
  default     = true
}
variable "existing_container_environment_id" {
  description = "Existing Container Apps environment ARM ID."
  type        = string
  default     = ""
}
variable "create_policy_engine" {
  description = "Create the policy engine Container App; false uses an existing app (configured separately)."
  type        = bool
  default     = true
}
variable "existing_policy_engine_id" {
  description = "Existing policy engine Container App ARM ID."
  type        = string
  default     = ""
}
variable "container_image" {
  description = "Optional already-built policy engine image. Empty deploys a bootstrap image until build-lab.sh updates it."
  type        = string
  default     = ""
}
variable "create_cosmos" {
  description = "Create single-write-region Cosmos and required containers; false uses an existing account."
  type        = bool
  default     = true
}
variable "existing_cosmos_id" {
  description = "Existing Cosmos ARM ID; required aipolicy containers are provisioned by Terraform."
  type        = string
  default     = ""
}
variable "create_cosmos_schema" {
  description = "Create database and containers. Null follows create_cosmos; false uses an existing aipolicy schema without importing it."
  type        = bool
  default     = null
}
variable "create_redis" {
  description = "Create an internal password-protected Redis Container App; false uses an existing Redis endpoint."
  type        = bool
  default     = true
}
variable "existing_redis_connection_string" {
  description = "Existing Redis connection string. Include password or use Entra-compatible Redis with managed identity permissions."
  type        = string
  sensitive   = true
  default     = ""
}
variable "create_key_vault" {
  description = "Create Key Vault for test credentials; false uses existing_key_vault_id."
  type        = bool
  default     = true
}
variable "existing_key_vault_id" {
  description = "Existing Key Vault ARM ID; deployer needs permission to write lab secrets."
  type        = string
  default     = ""
}
variable "create_foundry" {
  description = "Create a Foundry AI Services account; false references existing_foundry_id."
  type        = bool
  default     = true
}
variable "existing_foundry_id" {
  description = "Existing Foundry/OpenAI account ARM ID."
  type        = string
  default     = ""
}
variable "create_foundry_project" {
  description = "Create a Foundry project (requires an AI Services account with project management enabled)."
  type        = bool
  default     = true
}
variable "create_model_deployment" {
  description = "Create the model deployment; false uses deployment_name in the existing account."
  type        = bool
  default     = true
}
variable "deployment_name" {
  description = "AI deployment exposed by the lab gateway."
  type        = string
  default     = "budget-chat"
}
variable "model_name" {
  description = "Model to deploy. Update the verified budget price book if changed."
  type        = string
  default     = "gpt-4.1-mini"
}
variable "model_version" {
  description = "Pinned model version."
  type        = string
  default     = "2025-04-14"
}
variable "model_sku" {
  description = "Model deployment billing SKU."
  type        = string
  default     = "GlobalStandard"
}
variable "model_capacity" {
  description = "Model deployment capacity in Azure quota units."
  type        = number
  default     = 1
}
variable "create_apim" {
  description = "Create APIM; false attaches the lab API to existing_apim_id. Existing APIM needs a system assigned identity."
  type        = bool
  default     = true
}
variable "existing_apim_id" {
  description = "Existing API Management service ARM ID."
  type        = string
  default     = ""
}
variable "apim_sku" {
  description = "APIM SKU; Consumption_0 keeps the test gateway inexpensive."
  type        = string
  default     = "Consumption_0"
}
variable "publisher_email" {
  description = "APIM publisher contact email."
  type        = string
}
variable "create_identity" {
  description = "Create API, gateway and test-client Entra apps; false requires existing_identity."
  type        = bool
  default     = true
}
variable "existing_identity" {
  description = "Bring existing apps with access_as_user scopes, required API roles and consent. IDs are application client IDs; secret is for the admin automation app."
  type        = object({ api_client_id = string, gateway_client_id = string, test_client_id = string, admin_client_id = string, admin_client_secret = string })
  sensitive   = true
  default     = null
  validation {
    condition     = var.create_identity || var.existing_identity != null
    error_message = "Provide existing_identity when create_identity=false."
  }
}
variable "admin_user_object_id" {
  description = "Entra user allowed to sign in to the administration dashboard."
  type        = string
}
variable "create_test_user" {
  description = "Create a dedicated Entra test user with a password stored in ignored state and Key Vault."
  type        = bool
  default     = true
}
variable "test_user_domain" {
  description = "Verified Entra domain for the dedicated test user."
  type        = string
  default     = ""
  validation {
    condition     = !var.create_test_user || can(regex("^[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$", var.test_user_domain))
    error_message = "Set a verified Entra test_user_domain when create_test_user=true."
  }
}
variable "existing_test_user_object_id" {
  description = "Existing test user object ID when create_test_user=false. Supply delegated tokens to the E2E script."
  type        = string
  default     = ""
}

variable "create_network" {
  description = "Create VNet and Container Apps/private endpoint subnets; false uses existing network IDs."
  type        = bool
  default     = true
}
variable "network_address_space" {
  description = "Lab VNet CIDR."
  type        = string
  default     = "10.82.0.0/16"
}
variable "apps_subnet_cidr" {
  description = "Dedicated Container Apps subnet CIDR, /27 or larger."
  type        = string
  default     = "10.82.0.0/23"
}
variable "endpoints_subnet_cidr" {
  description = "Private endpoint subnet CIDR."
  type        = string
  default     = "10.82.2.0/24"
}
variable "existing_vnet_id" {
  description = "Existing VNet ARM ID when create_network=false."
  type        = string
  default     = ""
}
variable "existing_apps_subnet_id" {
  description = "Existing dedicated Microsoft.App/environments delegated subnet when create_network=false."
  type        = string
  default     = ""
}
variable "existing_endpoints_subnet_id" {
  description = "Existing private endpoint subnet when create_network=false."
  type        = string
  default     = ""
}
variable "create_cosmos_private_endpoint" {
  description = "Create Cosmos private endpoint and DNS linkage; false requires existing reachable Cosmos networking."
  type        = bool
  default     = true
}
variable "create_cosmos_private_dns_zone" {
  description = "Create privatelink.documents.azure.com; false uses existing_cosmos_private_dns_zone_id."
  type        = bool
  default     = true
}
variable "existing_cosmos_private_dns_zone_id" {
  description = "Existing privatelink.documents.azure.com DNS zone ARM ID."
  type        = string
  default     = ""
}
variable "create_key_vault_private_endpoint" {
  description = "Create Key Vault private endpoint and DNS linkage; false uses existing vault networking."
  type        = bool
  default     = true
}
variable "create_key_vault_private_dns_zone" {
  description = "Create privatelink.vaultcore.azure.net; false uses existing_key_vault_private_dns_zone_id."
  type        = bool
  default     = true
}
variable "existing_key_vault_private_dns_zone_id" {
  description = "Existing privatelink.vaultcore.azure.net DNS zone ARM ID."
  type        = string
  default     = ""
}
variable "store_lab_credentials_in_key_vault" {
  description = "Store lab credentials through the ARM deployment API. Deployer needs vaults/secrets/write permission."
  type        = bool
  default     = true
}

variable "container_workload_profile_name" {
  description = "Container Apps workload profile name; null for an existing legacy Consumption-only environment."
  type        = string
  default     = "Consumption"
}
