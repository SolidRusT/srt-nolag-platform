# allow internet access to nat #1
resource "aws_route_table" "public_1_to_internet" {
  vpc_id = aws_vpc.project.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.default.id
  }

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-public-net-1-${var.env}"
  ))
}

resource "aws_route_table_association" "internet_for_public_1" {
  route_table_id = aws_route_table.public_1_to_internet.id
  subnet_id      = aws_subnet.public_nat_1.id
}

# allow internet access to nat #2
resource "aws_route_table" "public_2_to_internet" {
  vpc_id = aws_vpc.project.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.default.id
  }

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-public-net-2-${var.env}"
  ))
}
resource "aws_route_table_association" "internet_for_public_2" {
  route_table_id = aws_route_table.public_2_to_internet.id
  subnet_id      = aws_subnet.public_nat_2.id
}
