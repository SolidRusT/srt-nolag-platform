output jenkins_iam_profile_name {
  value = aws_iam_instance_profile.jenkins_profile.name
}

output admin_sg_id {
    value = aws_security_group.allow_admins.id
}

output jenkins_sg_id {
    value = aws_security_group.allow_jenkins.id
}