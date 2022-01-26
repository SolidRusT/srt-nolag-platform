# save state to s3
terraform {
  backend "s3" {}
}

# default provider
provider "aws" {
  region = var.region
}

# VPC Providers
provider "aws" {
  alias  = "us_west_2"
  region = "us-west-2"
}

# discover current identity and save as a map
data "aws_caller_identity" "current" {}

# declare some local vars and common tags
locals {
  account_id        = data.aws_caller_identity.current.account_id
  ec2_key_name      = "${var.project}-${var.env}"

  common_tags = {
    Project      = var.project
    Environment  = var.env
    CreatedBy    = "Terraform"
    CostCategory = "OpsConsole"
  }
}

########################
## US-EAST-2 GAME CONFIG
# security module
module "security_us_west_2" {
  providers          = {
    aws = aws.us_west_2
  }
  source             = "./modules/security"
  env                = var.env
  project            = var.project
  region             = "us-west-2"
  vpc_id             = module.vpc_us_west_2.id
  vpc_cidr_block     = module.vpc_us_west_2.cidr_block
  allowed_admin_ip_1 = "24.80.112.171/32"
  allowed_admin_ip_2 = "50.92.183.181/32"
  common_tags        = local.common_tags
}

# VPC Module
module "vpc_us_west_2" {
  providers          = {
    aws = aws.us_west_2
  }
  source            = "./modules/vpc/us-west-2"
  env               = var.env
  project           = var.project
  region            = "us-west-2"
  vpc_region_prefix = var.vpc_region_us_west_2_prefix
  common_tags       = local.common_tags
}

########################
## US-WEST-1 GAME CONFIG
# security module
module "security_us_west_1" {
  providers          = {
    aws = aws.us_west_1
  }
  source             = "./modules/security"
  env                = var.env
  project            = var.project
  region             = "us-west-1"
  vpc_id             = module.vpc_us_west_1.id
  vpc_cidr_block     = module.vpc_us_west_1.cidr_block
  allowed_admin_ip_1 = "24.80.112.171/32"
  allowed_admin_ip_2 = "50.92.183.181/32"
  common_tags        = local.common_tags
}

# VPC Module
module "vpc_us_west_1" {
  providers          = {
    aws = aws.us_west_1
  }
  source            = "./modules/vpc/us-west-1"
  env               = var.env
  project           = var.project
  region            = "us-west-1"
  vpc_region_prefix = var.vpc_region_us_west_1_prefix
  common_tags       = local.common_tags
}

#############################
## US-EAST-1 GAME CONFIG
# security module
module "security_us_east_1" {
  providers          = {
    aws = aws.us_east_1
  }
  source             = "./modules/security"
  env                = var.env
  project            = var.project
  region             = "us-east-1"
  vpc_id             = module.vpc_us_east_1.id
  vpc_cidr_block     = module.vpc_us_east_1.cidr_block
  allowed_admin_ip_1 = "24.80.112.171/32"
  allowed_admin_ip_2 = "50.92.183.181/32"
  common_tags        = local.common_tags
}

# VPC Module
module "vpc_us_east_1" {
  providers          = {
    aws = aws.us_east_1
  }
  source            = "./modules/vpc/us-east-1"
  env               = var.env
  project           = var.project
  region            = "us-east-1"
  vpc_region_prefix = var.vpc_region_us_east_1_prefix
  common_tags       = local.common_tags
}

#############################
## US-EAST-2 GAME CONFIG
# security module
module "security_us_east_2" {
  providers          = {
    aws = aws.us_east_2
  }
  source             = "./modules/security"
  env                = var.env
  project            = var.project
  region             = "us-east-2"
  vpc_id             = module.vpc_us_east_2.id
  vpc_cidr_block     = module.vpc_us_east_2.cidr_block
  allowed_admin_ip_1 = "24.80.112.171/32"
  allowed_admin_ip_2 = "50.92.183.181/32"
  common_tags        = local.common_tags
}

# VPC Module
module "vpc_us_east_2" {
  providers          = {
    aws = aws.us_east_2
  }
  source            = "./modules/vpc/us-east-2"
  env               = var.env
  project           = var.project
  region            = "us-east-2"
  vpc_region_prefix = var.vpc_region_us_east_2_prefix
  common_tags       = local.common_tags
}

#############################
## US-WEST-1 GAME CONFIG
# security module
module "security_eu_west_1" {
  providers          = {
    aws = aws.eu_west_1
  }
  source             = "./modules/security"
  env                = var.env
  project            = var.project
  region             = "eu-west-1"
  vpc_id             = module.vpc_eu_west_1.id
  vpc_cidr_block     = module.vpc_eu_west_1.cidr_block
  allowed_admin_ip_1 = "24.80.112.171/32"
  allowed_admin_ip_2 = "50.92.183.181/32"
  common_tags        = local.common_tags
}

# VPC Module
module "vpc_eu_west_1" {
  providers          = {
    aws = aws.eu_west_1
  }
  source            = "./modules/vpc/eu-west-1"
  env               = var.env
  project           = var.project
  region            = "eu-west-1"
  vpc_region_prefix = var.vpc_region_us_west_1_prefix
  common_tags       = local.common_tags
}

#############################
## AP-SOUTHEAST-2 GAME CONFIG
# security module
module "security_ap_southeast_2" {
  providers          = {
    aws = aws.ap_southeast_2
  }
  source             = "./modules/security"
  env                = var.env
  project            = var.project
  region             = "ap-southeast-2"
  vpc_id             = module.vpc_ap_southeast_2.id
  vpc_cidr_block     = module.vpc_ap_southeast_2.cidr_block
  allowed_admin_ip_1 = "24.80.112.171/32"
  allowed_admin_ip_2 = "50.92.183.181/32"
  common_tags        = local.common_tags
}

# VPC Module
module "vpc_ap_southeast_2" {
  providers          = {
    aws = aws.ap_southeast_2
  }
  source            = "./modules/vpc/ap-southeast-2"
  env               = var.env
  project           = var.project
  region            = "ap-southeast-2"
  vpc_region_prefix = var.vpc_region_ap_southeast_2_prefix
  common_tags       = local.common_tags
}
