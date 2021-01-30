variable "env" {
  description = "The name of the environment"
}

variable "project" {
  description = "The name of the project"
  default = "suparious"
}

variable "bucket_domain" {
  description = "The regional domain name of the bucket."
  type        = string
}

variable "region" {
  description = "(Optional) If specified, the AWS region this bucket should reside in. Otherwise, the region used by the callee."
  type        = string
  default     = "us-west-2"
}

variable "domain_name" {
  description = "The domain name for the bucket."
  type        = string
}

variable "s3_origin_id" {
  
}

variable "logs_bucket_domain" {
  
}

variable "acm_cert_arn" {
  
}

variable "common_tags" {
  description = "(Optional) A mapping of tags to assign to the bucket."
  type        = map(string)
  default     = {}
}
