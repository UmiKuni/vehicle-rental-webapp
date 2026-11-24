# Remote state stored in Azure Blob Storage with automatic blob lease locking.
# Override storage_account_name or per-environment key via -backend-config if needed.
terraform {
  backend "azurerm" {
    resource_group_name  = "rg-carrental-tfstate"
    storage_account_name = "carrentaltfstate"
    container_name       = "tfstate"
    key                  = "carrental.terraform.tfstate"
  }
}
