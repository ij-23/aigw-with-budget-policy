#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
if [[ ! -f iac/terraform.tfvars ]]; then
  echo "Create iac/terraform.tfvars from iac/terraform.tfvars.example first."
  exit 1
fi
terraform -chdir=iac init -input=false
terraform -chdir=iac fmt -check -recursive
terraform -chdir=iac validate
terraform -chdir=iac plan -input=false -out=lab.tfplan
terraform -chdir=iac apply -input=false -auto-approve lab.tfplan
chmod 600 iac/terraform.tfstate
node scripts/lab/build-lab.mjs
node scripts/lab/e2e-budget.mjs --verify-block
