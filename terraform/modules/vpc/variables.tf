variable "env" {
  description = "The name of the environment"
}

variable "project" {
  description = "The name of the project"
  default     = "suparious"
}

variable "region" {
  description = "(Optional) If specified, the AWS region this bucket should reside in. Otherwise, the region used by the callee."
  type        = string
  default     = "us-west-2"
}

variable "multi_az" {
  default = false
}

variable "rds" {
  default = false
}

variable "nat_mode" {
  // TODO allow to disable creation of any type nat
  description = "Could be 'gateway' or 'instance', read more: https://docs.aws.amazon.com/AmazonVPC/latest/UserGuide/vpc-nat-comparison.html"
  default     = "gateway"
}

variable "vpc_cidr_prefix" {
  default = "10.0"
}

variable "nat_instance" {
  default = false
}

variable "nat_instance_multi_az" {
  default = false
}

variable "nat_gw" {
  default = true
  // TODO nat_gw = "${ var.nat_mode == "gateway" ? true : false }"
}

variable "nat_gw_multi_az" {
  default = false
  // TODO nat_gw_multi_az = "${ local.nat_gw == false ? false : var.multi_az }"
}

variable "nat_instance_type" {
  default = "t2.small"
}

variable "common_tags" {
  description = "(Optional) A mapping of tags to assign to the bucket."
  type        = map(string)
  default     = {}
}
