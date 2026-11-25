variable "project_name" {
  description = "Short lowercase alphanumeric prefix for resources"
  type        = string
  default     = "carrental"

  validation {
    condition     = can(regex("^[a-z0-9]{3,16}$", var.project_name))
    error_message = "project_name must be 3-16 lowercase alphanumeric characters."
  }
}

variable "environment" {
  description = "Deployment environment ('staging' or 'production')"
  type        = string
  default     = "staging"

  validation {
    condition     = contains(["staging", "production"], var.environment)
    error_message = "Environment must be 'staging' or 'production'."
  }
}

variable "location" {
  description = "Azure region for all resources"
  type        = string
  default     = "southeastasia"
}

variable "name_suffix" {
  description = "Optional lowercase alphanumeric suffix to ensure global uniqueness for ACR, PostgreSQL, and Key Vault"
  type        = string
  default     = ""

  validation {
    condition     = can(regex("^([a-z0-9]{1,6})?$", var.name_suffix))
    error_message = "name_suffix must be empty or 1-6 lowercase alphanumeric characters."
  }
}

variable "azure_subscription_id" {
  description = "Target Azure Subscription ID"
  type        = string
  sensitive   = true
}

# --- Container Registry ---
variable "acr_sku" {
  description = "Azure Container Registry SKU"
  type        = string
  default     = "Basic"

  validation {
    condition     = contains(["Basic", "Standard", "Premium"], var.acr_sku)
    error_message = "acr_sku must be 'Basic', 'Standard', or 'Premium'."
  }
}

# --- PostgreSQL Flexible Server ---
variable "postgres_admin_username" {
  description = "PostgreSQL administrator login"
  type        = string
  default     = "pgadmin"
}

variable "postgres_admin_password" {
  description = "PostgreSQL administrator password"
  type        = string
  sensitive   = true

  validation {
    condition     = length(var.postgres_admin_password) >= 12
    error_message = "PostgreSQL admin password must be at least 12 characters."
  }
}

variable "postgres_sku" {
  description = "PostgreSQL Flexible Server SKU"
  type        = string
  default     = "B_Standard_B1ms"
}

variable "postgres_storage_mb" {
  description = "PostgreSQL storage size in MB"
  type        = number
  default     = 32768
}

variable "postgres_version" {
  description = "PostgreSQL major version"
  type        = string
  default     = "17"
}

variable "postgres_backup_retention_days" {
  description = "Automated backup retention period in days"
  type        = number
  default     = 7
}

variable "postgres_geo_redundant_backup_enabled" {
  description = "Enable geo-redundant backups for PostgreSQL Flexible Server"
  type        = bool
  default     = false
}

variable "postgres_zone" {
  description = "Optional availability zone for PostgreSQL Flexible Server (null lets Azure choose)"
  type        = string
  default     = null
}

variable "postgres_database_name" {
  description = "Application database name on PostgreSQL Flexible Server"
  type        = string
  default     = "carrental"
}

# --- JWT Authentication (Required by UsersModule.AddUsersAuthentication) ---
variable "jwt_signing_key" {
  description = "Symmetric signing key for JWT tokens (must be >= 32 characters)"
  type        = string
  sensitive   = true

  validation {
    condition     = length(var.jwt_signing_key) >= 32
    error_message = "UsersModule requires Authentication:Jwt:SigningKey to be at least 32 characters."
  }
}

variable "jwt_issuer" {
  description = "JWT Token Issuer"
  type        = string
  default     = "CarRental.Api"
}

variable "jwt_audience" {
  description = "JWT Token Audience"
  type        = string
  default     = "CarRental.Client"
}

# --- External Services (Upstash Redis, Cloudinary, CORS) ---
variable "upstash_redis_connection_string" {
  description = "Upstash Redis TLS connection string (StackExchange.Redis format). Leave empty to use in-memory fallback."
  type        = string
  sensitive   = true
  default     = ""
}

variable "cloudinary_cloud_name" {
  description = "Cloudinary cloud name for vehicle images and KYC documents"
  type        = string
  default     = ""
}

variable "cloudinary_api_key" {
  description = "Cloudinary API key"
  type        = string
  sensitive   = true
  default     = ""
}

variable "cloudinary_api_secret" {
  description = "Cloudinary API secret"
  type        = string
  sensitive   = true
  default     = ""
}

variable "cors_allowed_origins" {
  description = "Frontend origins allowed by CORS policy (e.g., https://carrental.vercel.app)"
  type        = list(string)
  default     = ["http://localhost:3000"]
}

# --- Container App Sizing & Probes ---
variable "container_image" {
  description = "Initial container image used during first Terraform apply (subsequent deploys are ignored via lifecycle)"
  type        = string
  default     = "mcr.microsoft.com/k8se/quickstart:latest"
}

variable "aspnetcore_environment" {
  description = "Optional ASPNETCORE_ENVIRONMENT override (e.g. 'Development' to expose Scalar). Null derives it from var.environment."
  type        = string
  default     = null

  validation {
    condition     = var.aspnetcore_environment == null || contains(["Development", "Staging", "Production"], coalesce(var.aspnetcore_environment, "Staging"))
    error_message = "aspnetcore_environment must be null, 'Development', 'Staging', or 'Production'."
  }
}

variable "container_app_cpu" {
  description = "vCPU allocation for CarRental.Api container"
  type        = number
  default     = 0.5
}

variable "container_app_memory" {
  description = "Memory allocation for CarRental.Api container"
  type        = string
  default     = "1Gi"
}

variable "container_app_min_replicas" {
  description = "Minimum replica count (0 enables scale-to-zero in staging)"
  type        = number
  default     = 0
}

variable "container_app_max_replicas" {
  description = "Maximum replica count"
  type        = number
  default     = 3
}

variable "enable_health_probes" {
  description = "Enable HTTP /health liveness and readiness probes (enable after deploying an image that serves /health)"
  type        = bool
  default     = false
}

# --- Observability & Key Vault ---
variable "log_analytics_retention_days" {
  description = "Log Analytics workspace retention in days"
  type        = number
  default     = 30
}

variable "key_vault_purge_protection_enabled" {
  description = "Enable purge protection on Azure Key Vault"
  type        = bool
  default     = false
}
