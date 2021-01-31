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

# VPC network
module "vpc" {
  source          = "./modules/vpc"
  env             = var.env
  project         = var.project
  region          = var.region
  multi_az        = false
  nat_gw_multi_az = false
  rds             = false
  nat_mode        = "gateway"
  vpc_cidr_prefix = var.vpc_cidr_prefix
  common_tags     = local.common_tags
}

# security module
module "security" {
  source             = "./modules/security"
  env                = var.env
  project            = var.project
  region             = var.region
  vpc_id             = module.vpc.vpc_id
  vpc_cidr_block     = module.vpc.vpc_cidr_block
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

#################
## Jenkins server
# User data and bootstrapping
data "template_cloudinit_config" "jenkins_config" {
  #gzip          = true
  base64_encode = true
  part {
    content_type = "text/x-shellscript"
    content  = file("${path.cwd}/templates/ec2_docker_install.tpl")
  }
}

# Select latest update in ami flavour
data "aws_ami" "ubuntu" {
  most_recent = true
  filter {
    name   = "name"
    values = ["ubuntu/images/hvm-ssd/ubuntu-focal-20.04-amd64-server-*"]
  }
  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
  owners = ["099720109477"] # Canonical
}

# Create the instance
resource "aws_instance" "jenkins" {
  ami           = data.aws_ami.ubuntu.id
  instance_type = "t3a.small"
  key_name      = local.ec2_key_name
  user_data     = data.template_cloudinit_config.jenkins_config.rendered
  vpc_security_group_ids = [
    module.security.admin_sg_id,
    module.security.jenkins_sg_id
  ]
  subnet_id            = module.vpc.subnet_public_2
  iam_instance_profile = module.security.jenkins_iam_profile_name
  root_block_device {
    volume_type           = "gp2"
    volume_size           = 60
    delete_on_termination = true
    encrypted             = false
    #kms_key_id =
  }
  associate_public_ip_address = true
  tags = merge(local.common_tags, map(
    "Name", "${var.project}-jenkins-${var.env}"
  ))
  volume_tags = merge(local.common_tags, map(
    "Name", "${var.project}-jenkins-${var.env}"
  ))
}
