resource "aws_vpc" "project" {
  cidr_block           = "${var.vpc_cidr_prefix}.0.0/22"
  enable_dns_hostnames = true
  enable_dns_support   = true
  tags = merge(var.common_tags, map(
    "Name", "${var.project}-${var.env}"
  ))
}

resource "aws_internet_gateway" "default" {
  vpc_id = aws_vpc.project.id
}
