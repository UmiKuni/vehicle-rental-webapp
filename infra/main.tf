locals {
  env_short            = var.environment == "production" ? "prod" : "stg"
  suffix_plain         = lower(var.name_suffix)
  suffix_hyphen        = local.suffix_plain != "" ? "-${local.suffix_plain}" : ""
  resource_prefix      = "${var.project_name}-${var.environment}"
  acr_name             = lower(replace("${var.project_name}${var.environment}${local.suffix_plain}acr", "-", ""))
  postgres_server_name = "${local.resource_prefix}-pgserver${local.suffix_hyphen}"
  key_vault_name       = "${var.project_name}-${local.env_short}-kv${local.suffix_hyphen}"

  redis_enabled = trimspace(var.upstash_redis_connection_string) != ""

  cloudinary_any = (
    trimspace(var.cloudinary_cloud_name) != "" ||
    trimspace(var.cloudinary_api_key) != "" ||
    trimspace(var.cloudinary_api_secret) != ""
  )
  cloudinary_enabled = (
    trimspace(var.cloudinary_cloud_name) != "" &&
    trimspace(var.cloudinary_api_key) != "" &&
    trimspace(var.cloudinary_api_secret) != ""
  )

  common_tags = {
    project     = var.project_name
    environment = var.environment
    managed_by  = "terraform"
  }
}

data "azurerm_client_config" "current" {}

# 1. Resource Group
resource "azurerm_resource_group" "main" {
  name     = "rg-${local.resource_prefix}"
  location = var.location
  tags     = local.common_tags
}

# 2. Azure Container Registry (ACR)
resource "azurerm_container_registry" "acr" {
  name                = local.acr_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = var.acr_sku
  admin_enabled       = true
  tags                = local.common_tags
}

# 3. PostgreSQL Flexible Server (v17 + PostGIS + btree_gist + uuid-ossp)
resource "azurerm_postgresql_flexible_server" "postgres" {
  name                          = local.postgres_server_name
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  version                       = var.postgres_version
  administrator_login           = var.postgres_admin_username
  administrator_password        = var.postgres_admin_password
  sku_name                      = var.postgres_sku
  storage_mb                    = var.postgres_storage_mb
  backup_retention_days         = var.postgres_backup_retention_days
  geo_redundant_backup_enabled  = var.postgres_geo_redundant_backup_enabled
  zone                          = var.postgres_zone
  public_network_access_enabled = true
  tags                          = local.common_tags

  # Azure auto-assigns an availability zone when var.postgres_zone is null;
  # ignoring drift prevents azurerm from attempting an unsupported in-place zone change.
  lifecycle {
    ignore_changes = [zone]
  }
}

resource "azurerm_postgresql_flexible_server_database" "carrental_db" {
  name      = var.postgres_database_name
  server_id = azurerm_postgresql_flexible_server.postgres.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "allow_azure" {
  name             = "AllowAzureServices"
  server_id        = azurerm_postgresql_flexible_server.postgres.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_postgresql_flexible_server_configuration" "extensions" {
  name      = "azure.extensions"
  server_id = azurerm_postgresql_flexible_server.postgres.id
  value     = "POSTGIS,BTREE_GIST,UUID-OSSP"
}

# 4. Log Analytics Workspace
resource "azurerm_log_analytics_workspace" "logs" {
  name                = "${local.resource_prefix}-logs"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "PerGB2018"
  retention_in_days   = var.log_analytics_retention_days
  tags                = local.common_tags
}

# 5. Container Apps Environment
resource "azurerm_container_app_environment" "env" {
  name                       = "${local.resource_prefix}-env"
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.logs.id
  tags                       = local.common_tags
}

# 6. Container App (CarRental.Api)
resource "azurerm_container_app" "api" {
  name                         = "${local.resource_prefix}-api"
  container_app_environment_id = azurerm_container_app_environment.env.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = local.common_tags

  # No managed identity: Container Apps Express environments reject
  # System-assigned identities (ExpressSystemAssignedIdentityNotSupported).
  # Secrets are injected via Container App secrets below.

  registry {
    server               = azurerm_container_registry.acr.login_server
    username             = azurerm_container_registry.acr.admin_username
    password_secret_name = "acr-password"
  }

  secret {
    name  = "acr-password"
    value = azurerm_container_registry.acr.admin_password
  }

  secret {
    name  = "db-connection-string"
    value = "Host=${azurerm_postgresql_flexible_server.postgres.fqdn};Port=5432;Database=${var.postgres_database_name};Username=${var.postgres_admin_username};Password=${var.postgres_admin_password};SSL Mode=Require;Trust Server Certificate=true;"
  }

  secret {
    name  = "jwt-signing-key"
    value = var.jwt_signing_key
  }

  dynamic "secret" {
    for_each = local.redis_enabled ? [1] : []
    content {
      name  = "redis-connection-string"
      value = var.upstash_redis_connection_string
    }
  }

  dynamic "secret" {
    for_each = local.cloudinary_enabled ? [1] : []
    content {
      name  = "cloudinary-api-key"
      value = var.cloudinary_api_key
    }
  }

  dynamic "secret" {
    for_each = local.cloudinary_enabled ? [1] : []
    content {
      name  = "cloudinary-api-secret"
      value = var.cloudinary_api_secret
    }
  }

  template {
    min_replicas = var.container_app_min_replicas
    max_replicas = var.container_app_max_replicas

    container {
      name   = "carrental-api"
      image  = var.container_image
      cpu    = var.container_app_cpu
      memory = var.container_app_memory

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = coalesce(var.aspnetcore_environment, var.environment == "production" ? "Production" : "Staging")
      }

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }

      env {
        name        = "ConnectionStrings__DefaultConnection"
        secret_name = "db-connection-string"
      }

      dynamic "env" {
        for_each = local.redis_enabled ? [1] : []
        content {
          name        = "ConnectionStrings__Redis"
          secret_name = "redis-connection-string"
        }
      }

      env {
        name  = "Authentication__Jwt__Issuer"
        value = var.jwt_issuer
      }

      env {
        name  = "Authentication__Jwt__Audience"
        value = var.jwt_audience
      }

      env {
        name        = "Authentication__Jwt__SigningKey"
        secret_name = "jwt-signing-key"
      }

      dynamic "env" {
        for_each = local.cloudinary_enabled ? [1] : []
        content {
          name  = "Cloudinary__CloudName"
          value = var.cloudinary_cloud_name
        }
      }

      dynamic "env" {
        for_each = local.cloudinary_enabled ? [1] : []
        content {
          name        = "Cloudinary__ApiKey"
          secret_name = "cloudinary-api-key"
        }
      }

      dynamic "env" {
        for_each = local.cloudinary_enabled ? [1] : []
        content {
          name        = "Cloudinary__ApiSecret"
          secret_name = "cloudinary-api-secret"
        }
      }

      dynamic "env" {
        for_each = { for idx, origin in var.cors_allowed_origins : idx => origin }
        content {
          name  = "Cors__AllowedOrigins__${env.key}"
          value = env.value
        }
      }

      dynamic "liveness_probe" {
        for_each = var.enable_health_probes ? [1] : []
        content {
          transport = "HTTP"
          path      = "/health"
          port      = 8080
        }
      }

      dynamic "readiness_probe" {
        for_each = var.enable_health_probes ? [1] : []
        content {
          transport = "HTTP"
          path      = "/health"
          port      = 8080
        }
      }
    }

    http_scale_rule {
      name                = "http-scaling"
      concurrent_requests = "50"
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  lifecycle {
    #ignore_changes = [
    #  template[0].container[0].image
    #]

    precondition {
      condition     = !local.cloudinary_any || local.cloudinary_enabled
      error_message = "Cloudinary requires cloudinary_cloud_name, cloudinary_api_key, and cloudinary_api_secret to all be set together or all left empty."
    }
  }
}

# 7. Azure Key Vault (Azure RBAC)
resource "azurerm_key_vault" "vault" {
  name                       = local.key_vault_name
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true
  purge_protection_enabled   = var.key_vault_purge_protection_enabled
  soft_delete_retention_days = 7
  tags                       = local.common_tags
}

resource "azurerm_role_assignment" "key_vault_deployer_secrets_officer" {
  scope                = azurerm_key_vault.vault.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}
