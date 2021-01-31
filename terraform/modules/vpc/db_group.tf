resource "aws_db_subnet_group" "default" {
  count = var.rds ? 1 : 0
  name  = "rds"
  subnet_ids = [
    aws_subnet.private_rds_1[count.index].id,
    aws_subnet.private_rds_2[count.index].id
  ]
}
