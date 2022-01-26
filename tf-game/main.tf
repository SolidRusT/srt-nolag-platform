# save state to s3
terraform {
  backend "s3" {}
}

# default provider
provider "aws" {
  region = var.region
}

# for managing certificates
provider "aws" {
  alias  = "acm"
  region = "us-east-1"
}

# VPC Providers
provider "aws" {
  alias  = "us_west_2"
  region = "us-west-2"
}
provider "aws" {
  alias  = "us_west_1"
  region = "us-west-1"
}
provider "aws" {
  alias  = "us_east_1"
  region = "us-east-1"
}
provider "aws" {
  alias  = "us_east_2"
  region = "us-east-2"
}
provider "aws" {
  alias  = "eu_west_1"
  region = "eu-west-1"
}
provider "aws" {
  alias  = "ap_southeast_2"
  region = "ap-southeast-2"
}

# discover current identity and save as a map
data "aws_caller_identity" "current" {}

# external data
#data "external" "script" {
#  program = ["bash", "./get_ip.sh"] // get_ip.sh is your script name
#}

# declare some local vars and common tags
locals {
  account_id        = data.aws_caller_identity.current.account_id
  cloudwatch_prefix = "/${var.project}/${var.env}"
  bucket_arn        = module.main_s3.s3_bucket_arn
  s3_origin_id      = "${var.project}-${var.env}"
  ec2_key_name      = "${var.project}-${var.env}"

  common_tags = {
    Project      = var.project
    Environment  = var.env
    CreatedBy    = "Terraform"
    CostCategory = "SolidRust"
  }
}

# main web content storage
module "main_s3" {
  source        = "./modules/s3"
  env           = var.env
  project       = var.project
  region        = var.region
  bucket        = var.bucket
  acl           = "public-read"
  force_destroy = true
  versioning = {
    enabled = true
  }
  website = {
    index_document = "index.html"
    error_document = "shit.html"

  }
  logging = {
    target_bucket = module.logging_s3.s3_bucket_id
    target_prefix = "${var.project}-${var.env}"
  }
  server_side_encryption_configuration = {
    rule = {
      apply_server_side_encryption_by_default = {
        sse_algorithm = "AES256"
      }
    }
  }
  common_tags = local.common_tags
  depends_on = [
    module.logging_s3,
  ]
}

# main web logs storage
module "logging_s3" {
  source        = "./modules/s3-logs"
  env           = var.env
  project       = var.project
  region        = var.region
  bucket        = "${var.bucket}-logs"
  force_destroy = true
  website = {
    index_document = "index.html"
    error_document = "shit.html"

  }
  server_side_encryption_configuration = {
    rule = {
      apply_server_side_encryption_by_default = {
        sse_algorithm = "AES256"
      }
    }
  }
  common_tags = local.common_tags
}

# Backups s3
module "backups_s3" {
  source        = "./modules/s3"
  env           = var.env
  project       = var.project
  region        = var.region
  bucket        = "${var.bucket}-backups"
  acl           = "private"
  force_destroy = true
  versioning = {
    enabled = true
  }
  server_side_encryption_configuration = {
    rule = {
      apply_server_side_encryption_by_default = {
        sse_algorithm = "AES256"
      }
    }
  }
  common_tags = local.common_tags
}

# main SSL Certificate
resource "aws_acm_certificate" "cert" {
  provider = aws.acm

  domain_name               = var.domain_name
  subject_alternative_names = ["*.${var.domain_name}"]
  validation_method         = "DNS"

  lifecycle {
    create_before_destroy = true
  }

  tags = merge(local.common_tags, map(
    "Name", "${var.project}-${var.env}-dist"
  ))
}

# main cloudfront distribution
module "cloudfront" {
  source             = "./modules/cloudfront"
  env                = var.env
  project            = var.project
  region             = var.region
  domain_name        = var.domain_name
  bucket_domain      = module.main_s3.s3_bucket_regional_domain_name
  logs_bucket_domain = module.logging_s3.s3_bucket_regional_domain_name
  s3_origin_id       = local.s3_origin_id
  acm_cert_arn       = aws_acm_certificate.cert.arn
  common_tags        = local.common_tags
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
