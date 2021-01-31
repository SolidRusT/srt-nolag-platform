#!/bin/bash
# configure
export environment=$1
export project="suparious"
if [ -z ${environment} ]; then
  export environment="dev"
fi
echo "Running for ${environment}."
export region="us-west-2"
export bucket="suparious-tf-states-${region}"
export s3_key="${project}/${environment}/${project}-${environment}.tfstat"
export tf_plan_file=".terraform/latest-plan"
export tf_override_vars=""
export tf_vars_file="${environment}.tfvars"

echo "Backend: s3://${bucket}/${s3_key}"
echo "Vars file: ${tf_vars_file}"

# init environment
echo "Wiping out previous local state"
rm -rf .terraform
echo "Installing configured Terraform version"
tfenv install
echo "Activating configured Terraform version"
tfenv use
echo "activating execution break on fail"
set -e          # stop execution on failure

# check
echo "checking installed terraform version"
#INSTALLED=$(terraform version | head -n 1 | sed 's/Terraform v//')
echo "making fuckyou file"
terraform version | head -n 1 > fuckyou.txt
echo "cleaning the var"
INSTALLED=$(cat fuckyou.txt | sed 's/Terraform v//')
echo "removing fuckyou file"
rm fuckyou.txt
echo "declaring desired version"
DESIRED=$(cat .terraform-version)
echo "comparing installed and desired versions"
if [ "$INSTALLED" = "$DESIRED" ]; then
    echo "Terraform version is matching"
else
    echo "ERROR: Terraform version mismatch detected"
    echo "ERROR: Found version: $INSTALLED, but expected version: $DESIRED."
    exit 1
fi

# init terraform
echo "initializing terraform state"
terraform init \
-backend-config="bucket=${bucket}" \
-backend-config="key=${s3_key}" \
-backend-config="region=${region}" \
-backend=true \
-force-copy \
-get=true \
-input=false

# plan terraform changes
echo "planning terraform changes"
terraform plan \
-var-file="${tf_vars_file}" ${tf_override_vars} -out ${tf_plan_file}

## confirm terraform deployment
#read -p "Please confirm the terraform APPLY changes above: " -n 1 -r
#echo    # move to a new line
#if [[ ! $REPLY =~ ^[Yy]$ ]]
#then
#    exit 1
#fi

# apply terraform changes
echo "applying terraform changes"
terraform apply --input=false ${tf_plan_file}

set +e          # return to default shell behavior (continue on failure)

exit 0
