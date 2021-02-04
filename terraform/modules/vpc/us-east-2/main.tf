# us-east-2 network block
resource "aws_vpc" "us_east_2" {
  cidr_block                       = "${var.vpc_region_prefix}.0/24"
  enable_dns_hostnames             = true
  enable_dns_support               = true
  assign_generated_ipv6_cidr_block = false
  tags = merge(var.common_tags, map(
    "Name", "${var.project}-${var.env}"
  ))
}

# us-east-2 subnets
resource "aws_subnet" "us_east_2a" {
  cidr_block        = "${var.vpc_region_prefix}.0/28"
  vpc_id            = aws_vpc.us_east_2.id
  availability_zone = "us-east-2a"

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-us_east_2a-${var.env}"
  ))
}
resource "aws_subnet" "us_east_2b" {
  cidr_block        = "${var.vpc_region_prefix}.16/28"
  vpc_id            = aws_vpc.us_east_2.id
  availability_zone = "us-east-2b"

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-us_east_2b-${var.env}"
  ))
}
resource "aws_subnet" "us_east_2c" {
  cidr_block        = "${var.vpc_region_prefix}.32/28"
  vpc_id            = aws_vpc.us_east_2.id
  availability_zone = "us-east-2c"

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-us_east_2c-${var.env}"
  ))
}
#resource "aws_subnet" "us_east_2d" {
#  cidr_block        = "${var.vpc_region_prefix}.48/28"
#  vpc_id            = aws_vpc.us_east_2.id
#  availability_zone = "us-east-2d"
#
#  tags = merge(var.common_tags, map(
#    "Name", "${var.project}-us_east_2d-${var.env}"
#  ))
#}

# us-east-2 Internet gateway
resource "aws_internet_gateway" "us_east_2" {
  vpc_id = aws_vpc.us_east_2.id
}

# us-east-2 Internet router
resource "aws_route_table" "public_us_east_2" {
  vpc_id = aws_vpc.us_east_2.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.us_east_2.id
  }

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-public-us_east_2-${var.env}"
  ))
}

# us_east_2 subnet-router associations
resource "aws_route_table_association" "public-us_east_2a" {
  route_table_id = aws_route_table.public_us_east_2.id
  subnet_id      = aws_subnet.us_east_2a.id
}
resource "aws_route_table_association" "public-us_east_2b" {
  route_table_id = aws_route_table.public_us_east_2.id
  subnet_id      = aws_subnet.us_east_2b.id
}
resource "aws_route_table_association" "public-us_east_2c" {
  route_table_id = aws_route_table.public_us_east_2.id
  subnet_id      = aws_subnet.us_east_2c.id
}
#resource "aws_route_table_association" "public-us_east_2d" {
#  route_table_id = aws_route_table.public_us_east_2.id
#  subnet_id      = aws_subnet.us_east_2d.id
#}
