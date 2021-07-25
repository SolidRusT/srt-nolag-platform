# us-east-1 network block
resource "aws_vpc" "us_east_1" {
  cidr_block                       = "${var.vpc_region_prefix}.0/24"
  enable_dns_hostnames             = true
  enable_dns_support               = true
  assign_generated_ipv6_cidr_block = false
  tags = merge(var.common_tags, map(
    "Name", "${var.project}-${var.env}"
  ))
}

# us-east-1 subnets
resource "aws_subnet" "us_east_1a" {
  cidr_block        = "${var.vpc_region_prefix}.0/28"
  vpc_id            = aws_vpc.us_east_1.id
  availability_zone = "us-east-1a"

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-us_east_1a-${var.env}"
  ))
}
resource "aws_subnet" "us_east_1b" {
  cidr_block        = "${var.vpc_region_prefix}.16/28"
  vpc_id            = aws_vpc.us_east_1.id
  availability_zone = "us-east-1b"

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-us_east_1b-${var.env}"
  ))
}
resource "aws_subnet" "us_east_1c" {
  cidr_block        = "${var.vpc_region_prefix}.32/28"
  vpc_id            = aws_vpc.us_east_1.id
  availability_zone = "us-east-1c"

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-us_east_1c-${var.env}"
  ))
}
resource "aws_subnet" "us_east_1d" {
  cidr_block        = "${var.vpc_region_prefix}.48/28"
  vpc_id            = aws_vpc.us_east_1.id
  availability_zone = "us-east-1d"

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-us_east_1d-${var.env}"
  ))
}

# us-east-1 Internet gateway
resource "aws_internet_gateway" "us_east_1" {
  vpc_id = aws_vpc.us_east_1.id
}

# us-east-1 Internet router
resource "aws_route_table" "public_us_east_1" {
  vpc_id = aws_vpc.us_east_1.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.us_east_1.id
  }

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-public-us_east_1-${var.env}"
  ))
}

# us_east_1 subnet-router associations
resource "aws_route_table_association" "public-us_east_1a" {
  route_table_id = aws_route_table.public_us_east_1.id
  subnet_id      = aws_subnet.us_east_1a.id
}
resource "aws_route_table_association" "public-us_east_1b" {
  route_table_id = aws_route_table.public_us_east_1.id
  subnet_id      = aws_subnet.us_east_1b.id
}
resource "aws_route_table_association" "public-us_east_1c" {
  route_table_id = aws_route_table.public_us_east_1.id
  subnet_id      = aws_subnet.us_east_1c.id
}
resource "aws_route_table_association" "public-us_east_1d" {
  route_table_id = aws_route_table.public_us_east_1.id
  subnet_id      = aws_subnet.us_east_1d.id
}
