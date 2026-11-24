output "resource_group_name" {
  description = "Azure Resource Group name"
  value       = azurerm_resource_group.main.name
}

output "acr_login_server" {
  description = "Azure Container Registry login server"
  value       = azurerm_container_registry.acr.login_server
}

output "acr_admin_username" {
  description = "Azure Container Registry admin username"
  value       = azurerm_container_registry.acr.admin_username
  sensitive   = true
}

output "postgres_fqdn" {
  description = "PostgreSQL Flexible Server FQDN"
  value       = azurerm_postgresql_flexible_server.postgres.fqdn
}

output "postgres_database_name" {
  description = "PostgreSQL application database name"
  value       = azurerm_postgresql_flexible_server_database.carrental_db.name
}

output "redis_provider" {
  description = "Configured Redis provider mode"
  value       = nonsensitive(local.redis_enabled) ? "Upstash Redis (External Serverless TLS)" : "In-Memory Fallback (No Redis connection string configured)"
}

output "container_app_environment_name" {
  description = "Azure Container Apps Environment name"
  value       = azurerm_container_app_environment.env.name
}

output "container_app_name" {
  description = "Azure Container App name"
  value       = azurerm_container_app.api.name
}

output "container_app_url" {
  description = "Public HTTPS URL of CarRental.Api"
  value       = "https://${azurerm_container_app.api.ingress[0].fqdn}"
}

output "key_vault_name" {
  description = "Azure Key Vault name"
  value       = azurerm_key_vault.vault.name
}

output "key_vault_uri" {
  description = "Azure Key Vault URI"
  value       = azurerm_key_vault.vault.vault_uri
}
