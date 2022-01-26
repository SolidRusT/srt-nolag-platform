# us-west-2 network block
resource "aws_vpc" "us_west_2" {
  cidr_block                       = "${var.vpc_region_prefix}.0/24"
  enable_dns_hostnames             = true
  enable_dns_support               = true
  assign_generated_ipv6_cidr_block = false
  tags = merge(var.common_tags, map(
    "Name", "${var.project}-${var.env}"
  ))
}

# us-west-2 subnets
resource "aws_subnet" "us_west_2a" {
  cidr_block        = "${var.vpc_region_prefix}.0/28"
  vpc_id            = aws_vpc.us_west_2.id
  availability_zone = "us-west-2a"

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-us_west_2a-${var.env}"
  ))
}
resource "aws_subnet" "us_west_2b" {
  cidr_block        = "${var.vpc_region_prefix}.16/28"
  vpc_id            = aws_vpc.us_west_2.id
  availability_zone = "us-west-2b"

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-us_west_2b-${var.env}"
  ))
}
resource "aws_subnet" "us_west_2c" {
  cidr_block        = "${var.vpc_region_prefix}.32/28"
  vpc_id            = aws_vpc.us_west_2.id
  availability_zone = "us-west-2c"

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-us_west_2c-${var.env}"
  ))
}
resource "aws_subnet" "us_west_2d" {
  cidr_block        = "${var.vpc_region_prefix}.48/28"
  vpc_id            = aws_vpc.us_west_2.id
  availability_zone = "us-west-2d"

  tags = merge(var.common_tags, map(
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

  tags = merge(var.common_tags, map(
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
