# eu-west-1 network block
resource "aws_vpc" "eu_west_1" {
  cidr_block                       = "${var.vpc_region_prefix}.0/24"
  enable_dns_hostnames             = true
  enable_dns_support               = true
  assign_generated_ipv6_cidr_block = false
  tags = merge(var.common_tags, map(
    "Name", "${var.project}-${var.env}"
  ))
}

# eu-west-1 subnets
resource "aws_subnet" "eu_west_1a" {
  cidr_block        = "${var.vpc_region_prefix}.0/28"
  vpc_id            = aws_vpc.eu_west_1.id
  availability_zone = "eu-west-1a"

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-eu_west_1a-${var.env}"
  ))
}
resource "aws_subnet" "eu_west_1b" {
  cidr_block        = "${var.vpc_region_prefix}.16/28"
  vpc_id            = aws_vpc.eu_west_1.id
  availability_zone = "eu-west-1b"

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-eu_west_1b-${var.env}"
  ))
}
resource "aws_subnet" "eu_west_1c" {
  cidr_block        = "${var.vpc_region_prefix}.32/28"
  vpc_id            = aws_vpc.eu_west_1.id
  availability_zone = "eu-west-1c"

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-eu_west_1c-${var.env}"
  ))
}

# eu-west-1 Internet gateway
resource "aws_internet_gateway" "eu_west_1" {
  vpc_id = aws_vpc.eu_west_1.id
}

# eu-west-1 Internet router
resource "aws_route_table" "public_eu_west_1" {
  vpc_id = aws_vpc.eu_west_1.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.eu_west_1.id
  }

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-public-eu_west_1-${var.env}"
  ))
}

# eu_west_1 subnet-router associations
resource "aws_route_table_association" "public-eu_west_1a" {
  route_table_id = aws_route_table.public_eu_west_1.id
  subnet_id      = aws_subnet.eu_west_1a.id
}
resource "aws_route_table_association" "public-eu_west_1b" {
  route_table_id = aws_route_table.public_eu_west_1.id
  subnet_id      = aws_subnet.eu_west_1b.id
}
resource "aws_route_table_association" "public-eu_west_1c" {
  route_table_id = aws_route_table.public_eu_west_1.id
  subnet_id      = aws_subnet.eu_west_1c.id
}
