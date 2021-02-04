variable "env" {
  description = "The name of the environment"
}

variable "project" {
  description = "The name of the project"
}

variable "vpc_region_prefix" {
  description = "The name of the project"
}

variable "region" {
  description = "(Optional) If specified, the AWS region this bucket should reside in. Otherwise, the region used by the callee."
  type        = string
  default     = null
}

variable "common_tags" {
  description = "(Optional) A mapping of tags to assign to the bucket."
  type        = map(string)
  default     = {}
}
