<?php
$rust_server=$_GET['rust_server'];
?>
<html>
<head>
<meta charset="utf-8">
<title>Untitled Document</title>
</head>

<body>
	Server status for: 
	<?php
	echo "$rust_server.solidrust.net"
	?>
	<br><br>Hosted by: 
	<?php
echo $_SERVER['SERVER_NAME'];
?>
</body>
</html>