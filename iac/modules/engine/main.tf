resource "azurerm_user_assigned_identity" "engine" {
  count               = var.create ? 1 : 0
  name                = "${var.name_prefix}-engine-mi"
  resource_group_name = var.resource_group_name
  location            = var.location
  tags                = var.tags
}
resource "azurerm_role_assignment" "pull" {
  count                = var.create ? 1 : 0
  scope                = var.registry_id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.engine[0].principal_id
}
resource "azurerm_container_app" "engine" {
  count                        = var.create ? 1 : 0
  name                         = "${var.name_prefix}-engine"
  resource_group_name          = var.resource_group_name
  container_app_environment_id = var.environment_id
  revision_mode                = "Single"
  tags                         = var.tags
  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.engine[0].id]
  }
  registry {
    server   = var.registry_server
    identity = azurerm_user_assigned_identity.engine[0].id
  }
  secret {
    name  = "redis-connection"
    value = var.redis_connection_string
  }
  template {
    min_replicas = 1
    max_replicas = 2
    container {
      name   = "api"
      image  = var.container_image != "" ? var.container_image : "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
      cpu    = 0.5
      memory = "1Gi"
      env {
        name        = "ConnectionStrings__redis"
        secret_name = "redis-connection"
      }
      dynamic "env" {
        for_each = {
          ASPNETCORE_URLS                       = "http://+:8080"
          AZURE_CLIENT_ID                       = azurerm_user_assigned_identity.engine[0].client_id
          ConnectionStrings__aipolicy           = var.cosmos_endpoint
          AzureAd__Instance                     = "https://login.microsoftonline.com/"
          AzureAd__TenantId                     = var.tenant_id
          AzureAd__ClientId                     = var.api_client_id
          AzureAd__Audience                     = "api://${var.api_client_id}"
          AZURE_SUBSCRIPTION_ID                 = var.subscription_id
          AZURE_RESOURCE_GROUP                  = var.resource_group_name
          Foundry__SubscriptionIds__0           = var.subscription_id
          Foundry__ResourceIds__0               = var.foundry_id
          Apim__ResourceId                      = var.apim_id
          APPLICATIONINSIGHTS_CONNECTION_STRING = var.app_insights_connection_string
          EnableAgent365Exporter                = "false"
        }
        content {
          name  = env.key
          value = env.value
        }
      }
    }
  }
  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "http"
    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }
  depends_on = [azurerm_role_assignment.pull]
  lifecycle { ignore_changes = [template[0].container[0].image] }
}
data "azurerm_container_app" "existing" {
  count               = var.create ? 0 : 1
  name                = basename(var.existing_id)
  resource_group_name = split("/", var.existing_id)[4]
}
data "azurerm_user_assigned_identity" "existing" {
  count               = !var.create && data.azurerm_container_app.existing[0].identity[0].type == "UserAssigned" ? 1 : 0
  name                = basename(one(data.azurerm_container_app.existing[0].identity[0].identity_ids))
  resource_group_name = split("/", one(data.azurerm_container_app.existing[0].identity[0].identity_ids))[4]
}
