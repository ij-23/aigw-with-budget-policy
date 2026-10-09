terraform {
  required_version = ">= 1.9, < 2.0"
  required_providers {
    azurerm = { source = "hashicorp/azurerm", version = "~> 4.0" }
    azuread = { source = "hashicorp/azuread", version = "~> 3.0" }
    azapi   = { source = "Azure/azapi", version = "~> 2.0" }
    random  = { source = "hashicorp/random", version = "~> 3.0" }
  }
}

provider "azurerm" {
  subscription_id = var.subscription_id
  features {}
}
provider "azuread" { tenant_id = var.tenant_id }
provider "azapi" { subscription_id = var.subscription_id }
