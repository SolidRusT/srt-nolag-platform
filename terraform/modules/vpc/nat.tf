# Nat gateway #1
resource "aws_eip" "nat_1" {
  count = var.nat_gw ? 1 : 0
}
resource "aws_nat_gateway" "nat_1" {
  count         = var.nat_gw ? 1 : 0
  allocation_id = aws_eip.nat_1[count.index].id
  subnet_id     = aws_subnet.public_nat_1.id
}

# Nat gateway #2
resource "aws_eip" "nat_2" {
  count = var.nat_gw_multi_az ? 1 : 0
}
resource "aws_nat_gateway" "nat_2" {
  count = var.nat_gw_multi_az ? 1 : 0

  allocation_id = aws_eip.nat_2[count.index].id
  subnet_id     = aws_subnet.public_nat_2.id
}

// aws --region eu-west-1 ec2 describe-images --owners amazon --filters Name="name",Values="amzn-ami-vpc-nat*"
data "aws_ami" "nat" {
  most_recent = true

  filter {
    name   = "name"
    values = ["amzn-ami-vpc-nat*"]
  }

  owners = ["amazon"]
}

# Nat instance #1
resource "aws_instance" "nat_1" {
  count                       = var.nat_instance ? 1 : 0
  ami                         = data.aws_ami.nat.id
  instance_type               = var.nat_instance_type
  source_dest_check           = false
  subnet_id                   = aws_subnet.public_nat_1.id
  associate_public_ip_address = true
  vpc_security_group_ids      = [aws_security_group.access_via_nat[count.index].id]

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-nat-1-${var.env}"
  ))
}

# Nat instance #2
resource "aws_instance" "nat_2" {
  count                       = var.nat_instance_multi_az ? 1 : 0
  ami                         = data.aws_ami.nat.id
  instance_type               = var.nat_instance_type
  source_dest_check           = false
  subnet_id                   = aws_subnet.public_nat_2.id
  associate_public_ip_address = true
  vpc_security_group_ids      = [aws_security_group.access_via_nat[count.index].id]

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-nat-2-${var.env}"
  ))
}
