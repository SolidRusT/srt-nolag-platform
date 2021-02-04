# vpc-ap-southeast-2 network block
resource "aws_vpc" "ap_southeast_2" {
  cidr_block                       = "${var.vpc_region_ap_southeast_2_prefix}.0/24"
  enable_dns_hostnames             = true
  enable_dns_support               = true
  assign_generated_ipv6_cidr_block = false
  tags = merge(local.common_tags, map(
    "Name", "${var.project}-${var.env}"
  ))
}

# vpc-ap-southeast-2 subnets
resource "aws_subnet" "ap_southeast_2a" {
  cidr_block        = "${var.vpc_region_ap_southeast_2_prefix}.0/28"
  vpc_id            = aws_vpc.ap_southeast_2.id
  availability_zone = "vpc-ap-southeast-2a"

  tags = merge(local.common_tags, map(
    "Name", "${var.project}-ap_southeast_2a-${var.env}"
  ))
}
resource "aws_subnet" "ap_southeast_2b" {
  cidr_block        = "${var.vpc_region_ap_southeast_2_prefix}.16/28"
  vpc_id            = aws_vpc.ap_southeast_2.id
  availability_zone = "vpc-ap-southeast-2b"

  tags = merge(local.common_tags, map(
    "Name", "${var.project}-ap_southeast_2b-${var.env}"
  ))
}
resource "aws_subnet" "ap_southeast_2c" {
  cidr_block        = "${var.vpc_region_ap_southeast_2_prefix}.32/28"
  vpc_id            = aws_vpc.ap_southeast_2.id
  availability_zone = "vpc-ap-southeast-2c"

  tags = merge(local.common_tags, map(
    "Name", "${var.project}-ap_southeast_2c-${var.env}"
  ))
}

#resource "aws_subnet" "ap_southeast_2d" {
#  cidr_block        = "${var.vpc_region_ap_southeast_2_prefix}.48/28"
#  vpc_id            = aws_vpc.ap_southeast_2.id
#  availability_zone = "vpc-ap-southeast-2d"
#
#  tags = merge(local.common_tags, map(
#    "Name", "${var.project}-ap_southeast_2d-${var.env}"
#  ))
#}

# vpc-ap-southeast-2 Internet gateway
resource "aws_internet_gateway" "ap_southeast_2" {
  vpc_id = aws_vpc.ap_southeast_2.id
}

# vpc-ap-southeast-2 Internet router
resource "aws_route_table" "public_ap_southeast_2" {
  vpc_id = aws_vpc.ap_southeast_2.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.ap_southeast_2.id
  }

  tags = merge(local.common_tags, map(
    "Name", "${var.project}-public-ap_southeast_2-${var.env}"
  ))
}

# ap_southeast_2 subnet-router associations
resource "aws_route_table_association" "public-ap_southeast_2a" {
  route_table_id = aws_route_table.public_ap_southeast_2.id
  subnet_id      = aws_subnet.ap_southeast_2a.id
}
resource "aws_route_table_association" "public-ap_southeast_2b" {
  route_table_id = aws_route_table.public_ap_southeast_2.id
  subnet_id      = aws_subnet.ap_southeast_2b.id
}
resource "aws_route_table_association" "public-ap_southeast_2c" {
  route_table_id = aws_route_table.public_ap_southeast_2.id
  subnet_id      = aws_subnet.ap_southeast_2c.id
}
#resource "aws_route_table_association" "public-ap_southeast_2d" {
#  route_table_id = aws_route_table.public_ap_southeast_2.id
#  subnet_id      = aws_subnet.ap_southeast_2d.id
#}
