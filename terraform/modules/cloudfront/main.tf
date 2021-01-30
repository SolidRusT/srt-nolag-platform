# default provider
provider "aws" {
  region = var.region
}

# for managing certificates
provider "aws" {
  alias  = "acm"
  region = "us-east-1"
}

# discover current identity and save as a map
data "aws_caller_identity" "current" {}

# Origin access Identity
resource "aws_cloudfront_origin_access_identity" "origin_access_identity" {
  comment = "${var.project} ${var.env} identity"
}

# Website ditribution layer
resource "aws_cloudfront_distribution" "s3_distribution" {
  origin {
    domain_name = var.bucket_domain
    origin_id   = var.s3_origin_id

    s3_origin_config {
      origin_access_identity = "origin-access-identity/cloudfront/${aws_cloudfront_origin_access_identity.origin_access_identity.id}" ####WHUT
    }
  }

  enabled             = true
  is_ipv6_enabled     = true
  comment             = "${var.project} ${var.env} website"
  default_root_object = "index.html"

  logging_config {
    include_cookies = false
    bucket          = var.logs_bucket_domain
    prefix          = "cloudfront"
  }

  aliases = [var.domain_name, "*.${var.domain_name}"]

  custom_error_response {
    response_page_path = "/shit.html"
    error_code         = 400
    response_code      = 200
  }

  custom_error_response {
    response_page_path = "/shit.html"
    error_code         = 403
    response_code      = 200
  }

  custom_error_response {
    response_page_path = "/shit.html"
    error_code         = 404
    response_code      = 200
  }

  # , 403, 404, 405, 414, 416]

  default_cache_behavior {
    allowed_methods  = ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"]
    cached_methods   = ["GET", "HEAD"]
    target_origin_id = var.s3_origin_id

    forwarded_values {
      query_string = false

      cookies {
        forward = "none"
      }
    }

    viewer_protocol_policy = "allow-all"
    min_ttl                = 0
    default_ttl            = 3600
    max_ttl                = 86400
  }

  # Cache behavior with precedence 0
  ordered_cache_behavior {
    path_pattern     = "/content/immutable/*"
    allowed_methods  = ["GET", "HEAD", "OPTIONS"]
    cached_methods   = ["GET", "HEAD", "OPTIONS"]
    target_origin_id = var.s3_origin_id

    forwarded_values {
      query_string = false
      headers      = ["Origin"]

      cookies {
        forward = "none"
      }
    }

    min_ttl                = 0
    default_ttl            = 86400
    max_ttl                = 31536000
    compress               = true
    viewer_protocol_policy = "redirect-to-https"
  }

  # Cache behavior with precedence 1
  ordered_cache_behavior {
    path_pattern     = "/content/*"
    allowed_methods  = ["GET", "HEAD", "OPTIONS"]
    cached_methods   = ["GET", "HEAD"]
    target_origin_id = var.s3_origin_id

    forwarded_values {
      query_string = false

      cookies {
        forward = "none"
      }
    }

    min_ttl                = 0
    default_ttl            = 3600
    max_ttl                = 86400
    compress               = true
    viewer_protocol_policy = "redirect-to-https"
  }

  price_class = "PriceClass_200"

  restrictions {
    geo_restriction {
      restriction_type = "whitelist"
      locations        = ["US", "CA", "GB", "DE"]
    }
  }

  tags = merge(var.common_tags, map(
    "Name", "${var.project}-${var.env}-dist"
  ))

  viewer_certificate {
    acm_certificate_arn            = var.acm_cert_arn
    cloudfront_default_certificate = false
    minimum_protocol_version       = "TLSv1"
    ssl_support_method             = "sni-only"
  }
}