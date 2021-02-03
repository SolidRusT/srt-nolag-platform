output rust_iam_profile_name {
  value = aws_iam_instance_profile.rust_profile.name
}

output admin_sg_id {
    value = aws_security_group.allow_admins.id
}

output rust_sg_id {
    value = aws_security_group.allow_rust.id
}