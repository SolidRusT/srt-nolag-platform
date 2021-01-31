# allow internet access to Application nodes through nat #1
resource "aws_route_table" "nat_gw_1" {
  count  = var.nat_gw ? 1 : 0
  vpc_id = aws_vpc.project.id

  route {
    cidr_block     = "0.0.0.0/0"
    nat_gateway_id = aws_nat_gateway.nat_1[count.index].id
  }

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-nat-gw-1-${var.env}"
  ))
}
resource "aws_route_table_association" "app_1_subnet_to_nat_gw" {
  count          = var.nat_gw ? 1 : 0
  route_table_id = aws_route_table.nat_gw_1[count.index].id
  subnet_id      = aws_subnet.private_app_1.id
}

# allow internet access to Application nodes through nat #2
resource "aws_route_table" "nat_gw_2" {
  count  = var.nat_gw_multi_az ? 1 : 0
  vpc_id = aws_vpc.project.id

  route {
    cidr_block     = "0.0.0.0/0"
    nat_gateway_id = aws_nat_gateway.nat_2[count.index].id
  }

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-nat-gw-2-${var.env}"
  ))
}

resource "aws_route_table_association" "app_2_subnet_to_nat_gw" {
  count          = var.nat_gw_multi_az ? 1 : 0
  route_table_id = aws_route_table.nat_gw_2[count.index].id
  subnet_id      = aws_subnet.private_app_2[count.index].id
}
