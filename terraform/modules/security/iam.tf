# SolidRust IAM instance profile
resource "aws_iam_instance_profile" "rust_profile" {
  name = "rust_profile-${var.region}"
  role = aws_iam_role.rust_role.name
}

resource "aws_iam_role" "rust_role" {
  name = "rust_role-${var.region}"

  assume_role_policy = <<EOF
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Action": "sts:AssumeRole",
            "Principal": {
               "Service": "ec2.amazonaws.com"
            },
            "Effect": "Allow",
            "Sid": ""
        }
    ]
}
EOF
  tags = merge(var.common_tags, map(
    "Name", "${var.project}-${var.env}-dist"
  ))
}

resource "aws_iam_role_policy" "rust_s3_policy" {
  name = "s3_policy-${var.region}"
  role = aws_iam_role.rust_role.name

  policy = <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Action": [
        "s3:*"
      ],
      "Effect": "Allow",
      "Resource": "*"
    }
  ]
}
EOF
}
