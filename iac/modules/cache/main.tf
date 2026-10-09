resource "random_password" "redis" {
  count   = var.create ? 1 : 0
  length  = 32
  special = false
}
resource "azurerm_container_app" "redis" {
  count                        = var.create ? 1 : 0
  name                         = "${var.name_prefix}-redis"
  resource_group_name          = var.resource_group_name
  container_app_environment_id = var.environment_id
  revision_mode                = "Single"
  tags                         = var.tags
  secret {
    name  = "redis-password"
    value = random_password.redis[0].result
  }
  template {
    min_replicas = 1
    max_replicas = 1
    container {
      name    = "redis"
      image   = "redis:7.4-alpine"
      cpu     = 0.25
      memory  = "0.5Gi"
      command = ["sh", "-c"]
      args    = ["exec redis-server --requirepass \"$REDIS_PASSWORD\" --maxmemory 256mb --maxmemory-policy allkeys-lru"]
      env {
        name        = "REDIS_PASSWORD"
        secret_name = "redis-password"
      }
    }
  }
  ingress {
    external_enabled = false
    target_port      = 6379
    exposed_port     = 6379
    transport        = "tcp"
    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }
}
