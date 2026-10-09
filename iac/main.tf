data "azurerm_client_config" "current" {}
resource "azurerm_resource_group" "lab" {
  count    = var.create_resource_group ? 1 : 0
  name     = var.resource_group_name
  location = var.location
  tags     = var.tags
}
data "azurerm_resource_group" "existing" {
  count = var.create_resource_group ? 0 : 1
  name  = var.resource_group_name
}
locals {
  resource_group_name = var.create_resource_group ? azurerm_resource_group.lab[0].name : data.azurerm_resource_group.existing[0].name
}
module "monitoring" {
  source                    = "./modules/monitoring"
  create                    = var.create_monitoring
  name_prefix               = var.name_prefix
  resource_group_name       = local.resource_group_name
  location                  = var.location
  tags                      = var.tags
  existing_log_analytics_id = var.existing_log_analytics_id
  existing_app_insights_id  = var.existing_app_insights_id
}
module "hosting" {
  source                  = "./modules/hosting"
  create_registry         = var.create_registry
  create_environment      = var.create_container_environment
  existing_registry_id    = var.existing_registry_id
  existing_environment_id = var.existing_container_environment_id
  name_prefix             = var.name_prefix
  resource_group_name     = local.resource_group_name
  location                = var.location
  tags                    = var.tags
  log_analytics_id        = module.monitoring.workspace_id
}
module "cache" {
  source                     = "./modules/cache"
  create                     = var.create_redis
  existing_connection_string = var.existing_redis_connection_string
  environment_id             = module.hosting.environment_id
  resource_group_name        = local.resource_group_name
  name_prefix                = var.name_prefix
  tags                       = var.tags
}
module "cosmos" {
  source              = "./modules/cosmos"
  create              = var.create_cosmos
  create_schema       = coalesce(var.create_cosmos_schema, var.create_cosmos)
  existing_id         = var.existing_cosmos_id
  name_prefix         = var.name_prefix
  resource_group_name = local.resource_group_name
  location            = var.location
  tags                = var.tags
  log_analytics_id    = module.monitoring.workspace_id
}
module "foundry" {
  source              = "./modules/foundry"
  create              = var.create_foundry
  existing_id         = var.existing_foundry_id
  create_project      = var.create_foundry_project
  create_deployment   = var.create_model_deployment
  name_prefix         = var.name_prefix
  resource_group_name = local.resource_group_name
  location            = var.location
  tags                = var.tags
  deployment_name     = var.deployment_name
  model_name          = var.model_name
  model_version       = var.model_version
  model_sku           = var.model_sku
  model_capacity      = var.model_capacity
  log_analytics_id    = module.monitoring.workspace_id
}
module "identity" {
  source               = "./modules/identity"
  count                = var.create_identity ? 1 : 0
  name_prefix          = var.name_prefix
  admin_user_object_id = var.admin_user_object_id
}
locals {
  api_client_id       = var.create_identity ? module.identity[0].api_client_id : nonsensitive(var.existing_identity.api_client_id)
  gateway_client_id   = var.create_identity ? module.identity[0].gateway_client_id : nonsensitive(var.existing_identity.gateway_client_id)
  test_client_id      = var.create_identity ? module.identity[0].test_client_id : nonsensitive(var.existing_identity.test_client_id)
  admin_client_id     = var.create_identity ? module.identity[0].admin_client_id : nonsensitive(var.existing_identity.admin_client_id)
  admin_client_secret = var.create_identity ? module.identity[0].admin_client_secret : var.existing_identity.admin_client_secret
}
data "azuread_service_principal" "api" {
  client_id  = local.api_client_id
  depends_on = [module.identity]
}
module "gateway" {
  source              = "./modules/gateway"
  create              = var.create_apim
  existing_id         = var.existing_apim_id
  name_prefix         = var.name_prefix
  resource_group_name = local.resource_group_name
  location            = var.location
  tags                = var.tags
  sku                 = var.apim_sku
  publisher_email     = var.publisher_email
}
module "engine" {
  source                         = "./modules/engine"
  create                         = var.create_policy_engine
  existing_id                    = var.existing_policy_engine_id
  name_prefix                    = var.name_prefix
  resource_group_name            = local.resource_group_name
  location                       = var.location
  tags                           = var.tags
  environment_id                 = module.hosting.environment_id
  registry_id                    = module.hosting.registry_id
  registry_server                = module.hosting.registry_server
  container_image                = var.container_image
  redis_connection_string        = module.cache.connection_string
  cosmos_endpoint                = module.cosmos.endpoint
  app_insights_connection_string = module.monitoring.connection_string
  tenant_id                      = var.tenant_id
  api_client_id                  = local.api_client_id
  subscription_id                = var.subscription_id
  apim_id                        = module.gateway.id
  foundry_id                     = module.foundry.id
}
module "vault" {
  source              = "./modules/vault"
  create              = var.create_key_vault
  existing_id         = var.existing_key_vault_id
  name_prefix         = var.name_prefix
  resource_group_name = local.resource_group_name
  location            = var.location
  tags                = var.tags
  tenant_id           = var.tenant_id
  deployer_object_id  = data.azurerm_client_config.current.object_id
}
resource "random_password" "test_user" {
  count   = var.create_test_user ? 1 : 0
  length  = 28
  special = false
}
resource "azuread_user" "test" {
  count                 = var.create_test_user ? 1 : 0
  display_name          = "AI Gateway $50 Budget Test User"
  user_principal_name   = "${var.name_prefix}-test@${var.test_user_domain}"
  password              = random_password.test_user[0].result
  force_password_change = false
}
resource "azuread_application_redirect_uris" "dashboard" {
  count          = var.create_identity ? 1 : 0
  application_id = module.identity[0].api_application_id
  type           = "SPA"
  redirect_uris  = [module.engine.url, "http://localhost:5173"]
}
resource "azuread_app_role_assignment" "apim_engine" {
  app_role_id         = data.azuread_service_principal.api.app_role_ids["AIPolicy.Apim"]
  principal_object_id = module.gateway.principal_id
  resource_object_id  = data.azuread_service_principal.api.object_id
}
resource "azurerm_role_assignment" "apim_foundry" {
  scope                = module.foundry.id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = module.gateway.principal_id
}
resource "azurerm_role_assignment" "engine_foundry_reader" {
  scope                = module.foundry.id
  role_definition_name = "Reader"
  principal_id         = module.engine.principal_id
}
resource "azurerm_role_assignment" "engine_apim" {
  scope                = module.gateway.id
  role_definition_name = "API Management Service Contributor"
  principal_id         = module.engine.principal_id
}
resource "azurerm_cosmosdb_sql_role_assignment" "engine" {
  resource_group_name = module.cosmos.resource_group_name
  account_name        = module.cosmos.name
  role_definition_id  = "${module.cosmos.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
  principal_id        = module.engine.principal_id
  scope               = module.cosmos.id
}
locals {
  policy_parameters = {
    TenantId             = var.tenant_id
    ExpectedAudience     = local.gateway_client_id
    ContainerAppAudience = "api://${local.api_client_id}"
    ContainerAppUrl      = module.engine.url
  }
  rendered_policy = replace(replace(replace(replace(file("${path.module}/../policies/templates/entra-jwt-ai-budget/policy.xml"), "{{TenantId}}", local.policy_parameters.TenantId), "{{ExpectedAudience}}", local.policy_parameters.ExpectedAudience), "{{ContainerAppAudience}}", local.policy_parameters.ContainerAppAudience), "{{ContainerAppUrl}}", local.policy_parameters.ContainerAppUrl)
}
resource "azurerm_api_management_api" "budget" {
  name                  = "ai-budget-lab"
  resource_group_name   = module.gateway.resource_group_name
  api_management_name   = module.gateway.name
  revision              = "1"
  display_name          = "AI budget lab — $50 per user per month"
  path                  = "budget/openai"
  protocols             = ["https"]
  service_url           = "${trimsuffix(module.foundry.endpoint, "/")}/openai"
  subscription_required = false
}
resource "azurerm_api_management_api_operation" "chat" {
  operation_id        = "budget-chat"
  api_name            = azurerm_api_management_api.budget.name
  api_management_name = module.gateway.name
  resource_group_name = module.gateway.resource_group_name
  display_name        = "Budget-enforced chat completions"
  method              = "POST"
  url_template        = "/deployments/{deployment}/chat/completions"
  template_parameter {
    name     = "deployment"
    type     = "string"
    required = true
  }
}
resource "azurerm_api_management_api_policy" "budget" {
  api_name            = azurerm_api_management_api.budget.name
  api_management_name = module.gateway.name
  resource_group_name = module.gateway.resource_group_name
  xml_content         = local.rendered_policy
  depends_on          = [azurerm_api_management_api_operation.chat]
}
