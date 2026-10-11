#!/usr/bin/env bash
set -euo pipefail
umask 077
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
for tool in terraform az node; do
  command -v "$tool" >/dev/null || { echo "Missing prerequisite: $tool"; exit 1; }
done
node -e 'if(Number(process.versions.node.split(".")[0])<22)throw new Error("Node.js 22 or newer is required")'
if [[ ! -f iac/terraform.tfvars ]]; then
  echo "Run node scripts/lab/configure-lab.mjs, or copy iac/terraform.tfvars.example to iac/terraform.tfvars."
  exit 1
fi
terraform -chdir=iac init -input=false
terraform -chdir=iac fmt -check -recursive
terraform -chdir=iac validate
terraform -chdir=iac plan -input=false -out=lab.tfplan
terraform -chdir=iac apply -input=false -auto-approve lab.tfplan
chmod 600 iac/terraform.tfstate
node scripts/lab/build-lab.mjs
node scripts/lab/e2e-budget.mjs --setup-only
echo "POC deployment and budget setup complete."
echo "Next: node scripts/lab/e2e-budget.mjs --device-code --verify-block"
