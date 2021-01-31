#!/bin/bash
# configure
export environment=$1
export project="solidrust"
if [ -z ${environment} ]; then
  export environment="dev"
fi
echo "Running for ${environment}."
export region="us-west-2"
export bucket="${project}-tf-states-${region}"
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

# check for updates
echo "Checking for latest Terraform version"
LATEST_TF=$(terraform version | head -n 1 | sed 's/Terraform v//')
echo "Latest Terraform Version is: ${LATEST_TF} " 
DESIRED=$(cat .terraform-version)
echo "comparing latest and desired terraform versions"
if [ "$LATEST_TF" = "$DESIRED" ]; then
    echo "Terraform version is matching"
else
    echo "WARNING: Terraform version mismatch detected"
    echo "WARNING: Found desired version: $DESIRED, but expected latest version: $LATEST_TF."
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
