# CarRental Platform — Azure Infrastructure (Terraform)

Declarative Azure infrastructure for the `.NET 9` CarRental API (`be/src/Host/CarRental.Api/CarRental.Api.csproj`), designed for Azure for Students cost optimization (Container Apps Consumption + PostgreSQL Flexible Server `B_Standard_B1ms` + external Upstash Redis Free Tier).

## Files

- `infra/providers.tf` — Terraform (`>= 1.6.0`) and `hashicorp/azurerm` (`~> 4.0`) provider configuration
- `infra/backend.tf` — Azure Blob Storage remote state backend (`azurerm`)
- `infra/variables.tf` — Input variable definitions and validation rules
- `infra/main.tf` — Resource Group, ACR, PostgreSQL Flexible Server 17 (`POSTGIS`, `BTREE_GIST`, `UUID-OSSP`), Log Analytics, Container Apps Environment & App, and Key Vault
- `infra/outputs.tf` — Exported endpoints and resource identifiers
- `infra/terraform.tfvars.example` — Safe template for local secret/override configuration
- `infra/environments/staging.tfvars` — Non-secret staging overrides (scale-to-zero, `B_Standard_B1ms`, `Basic` ACR)
- `infra/environments/production.tfvars` — Non-secret production overrides (always-on `min_replicas = 1`, `GP_Standard_D2ds_v5`, geo-redundant backups, Key Vault purge protection)

## Prerequisites

- Terraform `>= 1.6.0`
- Azure CLI (`az login` and `az account set --subscription "<SUBSCRIPTION_ID>"`)
- Azure permissions: because Key Vault uses Azure RBAC (`rbac_authorization_enabled = true`), the deploying account or CI/CD Service Principal needs `Owner` or `Contributor` + `User Access Administrator` on the subscription/resource group to manage Key Vault role assignments

## 1. One-Time Remote State Bootstrap

Before running `terraform init` with the remote backend, create the state Storage Account and Blob Container:

```powershell
az group create --name rg-carrental-tfstate --location southeastasia

az storage account create `
  --name carrentaltfstate `
  --resource-group rg-carrental-tfstate `
  --location southeastasia `
  --sku Standard_LRS `
  --encryption-services blob `
  --min-tls-version TLS1_2 `
  --allow-blob-public-access false

az storage container create `
  --name tfstate `
  --account-name carrentaltfstate `
  --auth-mode login
```

If `carrentaltfstate` is already taken globally, pick a unique name and pass `-backend-config="storage_account_name=<your_unique_name>"` to `terraform init`.

## 2. Configure Variables

Copy `infra/terraform.tfvars.example` to `infra/terraform.tfvars` (gitignored) and fill in your secrets:

- `azure_subscription_id`
- `postgres_admin_password` (>= 12 characters)
- `jwt_signing_key` (>= 32 characters, required by `UsersModule`)
- `upstash_redis_connection_string` (optional — Upstash TLS string `host:6379,password=...,ssl=True,abortConnect=False`; omit to run with in-memory fallback)
- `cloudinary_cloud_name`, `cloudinary_api_key`, `cloudinary_api_secret` (optional — set all three together, or leave all empty for in-memory storage)
- `name_suffix` (optional 1–6 lowercase alphanumeric characters if default ACR/PostgreSQL/Key Vault names collide globally)

## 3. Plan & Apply

```powershell
cd infra
terraform init
terraform plan -var-file="environments/staging.tfvars" -out=tfplan
terraform apply tfplan
```

After the first apply:

1. Initialize extensions on the `carrental` database using `be/scripts/init-db.sql` (or EF Core migrations).
2. Build and push the `carrental-api` image to ACR and deploy it to the Container App.
3. Once `CarRental.Api` exposes `GET /health`, set `enable_health_probes = true` in `infra/terraform.tfvars` and re-apply so Container Apps enables HTTP liveness and readiness probes.
