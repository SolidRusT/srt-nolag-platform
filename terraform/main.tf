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

# us-west-2 network block
resource "aws_vpc" "us_west_2" {
  cidr_block                       = "${var.vpc_region_us_west_2_prefix}.0/24"
  enable_dns_hostnames             = true
  enable_dns_support               = true
  assign_generated_ipv6_cidr_block = false
  tags = merge(local.common_tags, map(
    "Name", "${var.project}-${var.env}"
  ))
}

# us-west-2 subnets
resource "aws_subnet" "us_west_2a" {
  cidr_block        = "${var.vpc_region_us_west_2_prefix}.0/28"
  vpc_id            = aws_vpc.us_west_2.id
  availability_zone = "us-west-2a"

  tags = merge(local.common_tags, map(
    "Name", "${var.project}-us_west_2a-${var.env}"
  ))
}
resource "aws_subnet" "us_west_2b" {
  cidr_block        = "${var.vpc_region_us_west_2_prefix}.16/28"
  vpc_id            = aws_vpc.us_west_2.id
  availability_zone = "us-west-2b"

  tags = merge(local.common_tags, map(
    "Name", "${var.project}-us_west_2b-${var.env}"
  ))
}
resource "aws_subnet" "us_west_2c" {
  cidr_block        = "${var.vpc_region_us_west_2_prefix}.32/28"
  vpc_id            = aws_vpc.us_west_2.id
  availability_zone = "us-west-2c"

  tags = merge(local.common_tags, map(
    "Name", "${var.project}-us_west_2c-${var.env}"
  ))
}
resource "aws_subnet" "us_west_2d" {
  cidr_block        = "${var.vpc_region_us_west_2_prefix}.48/28"
  vpc_id            = aws_vpc.us_west_2.id
  availability_zone = "us-west-2d"

  tags = merge(local.common_tags, map(
    "Name", "${var.project}-us_west_2d-${var.env}"
  ))
}

# us-west-2 Internet gateway
resource "aws_internet_gateway" "us_west_2" {
  vpc_id = aws_vpc.us_west_2.id
}

# us-west-2 Internet router
resource "aws_route_table" "public_us_west_2" {
  vpc_id = aws_vpc.us_west_2.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.us_west_2.id
  }

  tags = merge(local.common_tags, map(
    "Name", "${var.project}-public-us_west_2-${var.env}"
  ))
}

# us_west_2 subnet-router associations
resource "aws_route_table_association" "public-us_west_2a" {
  route_table_id = aws_route_table.public_us_west_2.id
  subnet_id      = aws_subnet.us_west_2a.id
}
resource "aws_route_table_association" "public-us_west_2b" {
  route_table_id = aws_route_table.public_us_west_2.id
  subnet_id      = aws_subnet.us_west_2b.id
}
resource "aws_route_table_association" "public-us_west_2c" {
  route_table_id = aws_route_table.public_us_west_2.id
  subnet_id      = aws_subnet.us_west_2c.id
}
resource "aws_route_table_association" "public-us_west_2d" {
  route_table_id = aws_route_table.public_us_west_2.id
  subnet_id      = aws_subnet.us_west_2d.id
}

#resource "aws_subnet" "us_west_2e" {
#  cidr_block        = "${var.vpc_region_us_west_2_prefix}.64/28"
#  vpc_id            = aws_vpc.us_west_2.id
#  availability_zone = "us-west-2e"
#
#  tags = merge(local.common_tags, map(
#    "Name", "${var.project}-us_west_2e-${var.env}"
#  ))
#}
#resource "aws_subnet" "us_west_2f" {
#  cidr_block        = "${var.vpc_region_us_west_2_prefix}.72/28"
#  vpc_id            = aws_vpc.us_west_2.id
#  availability_zone = "us-west-2f"
#
#  tags = merge(local.common_tags, map(
#    "Name", "${var.project}-us_west_2f-${var.env}"
#  ))
#}


# security module
module "security_us_west_2" {
  source             = "./modules/security"
  env                = var.env
  project            = var.project
  region             = "us-west-2"
  vpc_id             = aws_vpc.us_west_2.id
  vpc_cidr_block     = aws_vpc.us_west_2.cidr_block
  allowed_admin_ip_1 = "24.80.112.171/32"
  allowed_admin_ip_2 = "50.92.183.181/32"

  common_tags = local.common_tags
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
