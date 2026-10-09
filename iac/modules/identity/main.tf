data "azuread_client_config" "current" {}
resource "random_uuid" "scope" { for_each = toset(["api", "gateway"]) }
resource "random_uuid" "role" { for_each = toset(["AIPolicy.Admin", "AIPolicy.Apim", "AIPolicy.Export"]) }
resource "azuread_application" "api" {
  display_name = "${var.name_prefix} Policy Engine Dashboard"
  owners       = [data.azuread_client_config.current.object_id]
  api {
    requested_access_token_version = 2
    oauth2_permission_scope {
      id                         = random_uuid.scope["api"].result
      value                      = "access_as_user"
      type                       = "Admin"
      admin_consent_display_name = "Manage AI budgets"
      admin_consent_description  = "Manage the lab policy engine and view budget usage."
      enabled                    = true
    }
  }
  dynamic "app_role" {
    for_each = random_uuid.role
    content {
      id                   = app_role.value.result
      value                = app_role.key
      display_name         = app_role.key
      description          = "Policy engine ${app_role.key} access"
      allowed_member_types = app_role.key == "AIPolicy.Apim" ? ["Application"] : ["Application", "User"]
      enabled              = true
    }
  }
}
resource "azuread_application_identifier_uri" "api" {
  application_id = azuread_application.api.id
  identifier_uri = "api://${azuread_application.api.client_id}"
}
resource "azuread_service_principal" "api" { client_id = azuread_application.api.client_id }
resource "azuread_application" "gateway" {
  display_name = "${var.name_prefix} APIM AI Gateway"
  owners       = [data.azuread_client_config.current.object_id]
  api {
    requested_access_token_version = 2
    oauth2_permission_scope {
      id                         = random_uuid.scope["gateway"].result
      value                      = "access_as_user"
      type                       = "Admin"
      admin_consent_display_name = "Use AI through APIM"
      admin_consent_description  = "Use AI with a per-user USD budget enforced by APIM."
      enabled                    = true
    }
  }
}
resource "azuread_application_identifier_uri" "gateway" {
  application_id = azuread_application.gateway.id
  identifier_uri = "api://${azuread_application.gateway.client_id}"
}
resource "azuread_service_principal" "gateway" { client_id = azuread_application.gateway.client_id }
resource "azuread_application" "test" {
  display_name                   = "${var.name_prefix} Delegated Test Client"
  owners                         = [data.azuread_client_config.current.object_id]
  fallback_public_client_enabled = true
  public_client { redirect_uris = ["http://localhost"] }
  required_resource_access {
    resource_app_id = azuread_application.gateway.client_id
    resource_access {
      id   = random_uuid.scope["gateway"].result
      type = "Scope"
    }
  }
}
resource "azuread_service_principal" "test" { client_id = azuread_application.test.client_id }
resource "azuread_service_principal_delegated_permission_grant" "test" {
  service_principal_object_id          = azuread_service_principal.test.object_id
  resource_service_principal_object_id = azuread_service_principal.gateway.object_id
  claim_values                         = ["access_as_user"]
}
resource "azuread_service_principal_delegated_permission_grant" "dashboard" {
  service_principal_object_id          = azuread_service_principal.api.object_id
  resource_service_principal_object_id = azuread_service_principal.api.object_id
  claim_values                         = ["access_as_user"]
}
resource "azuread_application" "admin" {
  display_name = "${var.name_prefix} Lab Bootstrap"
  owners       = [data.azuread_client_config.current.object_id]
  required_resource_access {
    resource_app_id = azuread_application.api.client_id
    resource_access {
      id   = random_uuid.role["AIPolicy.Admin"].result
      type = "Role"
    }
  }
}
resource "azuread_service_principal" "admin" { client_id = azuread_application.admin.client_id }
resource "azuread_application_password" "admin" {
  application_id = azuread_application.admin.id
  display_name   = "Lab bootstrap (rotate after testing)"
  end_date       = timeadd(timestamp(), "720h")
  lifecycle { ignore_changes = [end_date] }
}
resource "azuread_app_role_assignment" "bootstrap" {
  principal_object_id = azuread_service_principal.admin.object_id
  resource_object_id  = azuread_service_principal.api.object_id
  app_role_id         = random_uuid.role["AIPolicy.Admin"].result
}
resource "azuread_app_role_assignment" "dashboard" {
  principal_object_id = var.admin_user_object_id
  resource_object_id  = azuread_service_principal.api.object_id
  app_role_id         = random_uuid.role["AIPolicy.Admin"].result
}
