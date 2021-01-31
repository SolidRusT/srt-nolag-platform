output "s3_bucket_id" {
  description = "The name of the bucket."
  value       = element(concat(aws_s3_bucket.s3_bucket.*.id, list("")), 0)
}

output "s3_bucket_arn" {
  description = "The ARN of the bucket. Will be of format arn:aws:s3:::bucketname."
  value       = element(concat(aws_s3_bucket.s3_bucket.*.arn, list("")), 0)
}

output "s3_bucket_website_endpoint" {
  description = "The website endpoint for the s3 bucket."
  value       = element(concat(aws_s3_bucket.s3_bucket.*.website_endpoint, list("")), 0)
}

output "s3_bucket_regional_domain_name" {
  description = "The regional bucket name for the s3 bucket."
  value       = element(concat(aws_s3_bucket.s3_bucket.*.bucket_regional_domain_name, list("")), 0)
}
