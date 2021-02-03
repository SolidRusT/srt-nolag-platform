#####################
## Allow Admin access
resource "aws_security_group" "allow_admins" {
  name        = "allow_admins"
  description = "Allow Suparious Admin inbound traffic"
  vpc_id      = var.vpc_id

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-${var.env}-sg-admins"
  ))
}

# Admin SSH rule
resource "aws_security_group_rule" "admin_ssh_access_1" {
  description       = "admin_ssh_access"
  type              = "ingress"
  from_port         = 22
  to_port           = 22
  protocol          = "tcp"
  cidr_blocks       = [var.allowed_admin_ip_1]
  security_group_id = aws_security_group.allow_admins.id
}

# Admin RDP rule
resource "aws_security_group_rule" "admin_rdp_access_1" {
  description       = "admin_rdp_access"
  type              = "ingress"
  from_port         = 3389
  to_port           = 3389
  protocol          = "tcp"
  cidr_blocks       = [var.allowed_admin_ip_1]
  security_group_id = aws_security_group.allow_admins.id
}

# Admin SSH rule
resource "aws_security_group_rule" "admin_ssh_access_2" {
  description       = "admin_ssh_access"
  type              = "ingress"
  from_port         = 22
  to_port           = 22
  protocol          = "tcp"
  cidr_blocks       = [var.allowed_admin_ip_2]
  security_group_id = aws_security_group.allow_admins.id
}

# Admin RDP rule
resource "aws_security_group_rule" "admin_rdp_access_2" {
  description       = "admin_rdp_access"
  type              = "ingress"
  from_port         = 3389
  to_port           = 3389
  protocol          = "tcp"
  cidr_blocks       = [var.allowed_admin_ip_2]
  security_group_id = aws_security_group.allow_admins.id
}

####################
## Allow Public Rust
resource "aws_security_group" "allow_rust" {
  name        = "allow_rust"
  description = "Allow SolidRust public inbound traffic"
  vpc_id      = var.vpc_id

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-${var.env}-sg-rust"
  ))
}

# Public Rust game rule
resource "aws_security_group_rule" "rust_game_access_1" {
  description       = "rust_game_access"
  type              = "ingress"
  from_port         = 28015
  to_port           = 28015
  protocol          = "udp"
  cidr_blocks       = ["0.0.0.0/0"]
  security_group_id = aws_security_group.allow_rust.id
}

# Public Rust webapp rule
resource "aws_security_group_rule" "rust_game_access_2" {
  description       = "rust_game_access"
  type              = "ingress"
  from_port         = 28082
  to_port           = 28082
  protocol          = "tcp"
  cidr_blocks       = ["0.0.0.0/0"]
  security_group_id = aws_security_group.allow_rust.id
}

# Admin RCON rule 1
resource "aws_security_group_rule" "admin_rcon_access_1" {
  description       = "rust_rcon_access"
  type              = "ingress"
  from_port         = 28016
  to_port           = 28016
  protocol          = "tcp"
  cidr_blocks       = [var.allowed_admin_ip_1]
  security_group_id = aws_security_group.allow_rust.id
}

# Admin RCON rule 2
resource "aws_security_group_rule" "admin_rcon_access_2" {
  description       = "rust_rcon_access"
  type              = "ingress"
  from_port         = 28016
  to_port           = 28016
  protocol          = "tcp"
  cidr_blocks       = [var.allowed_admin_ip_2]
  security_group_id = aws_security_group.allow_rust.id
}

# Rust Outbound
resource "aws_security_group_rule" "outbound_access" {
  description       = "rust_outbound_access"
  type              = "egress"
  from_port         = 0
  to_port           = 0
  protocol          = "-1"
  cidr_blocks       = ["0.0.0.0/0"]
  security_group_id = aws_security_group.allow_rust.id
}
