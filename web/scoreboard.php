<?php include 'nav_bar.php';?>
<?php
// Create connection to XPerience
$db_name = "XPerience";
$db_conn = "mysql:host=$db_host;dbname=$db_name";
$xpstatsdb = new PDO($db_conn, $user, $pass);
// Check connection
if ($xpstatsdb->connect_error) {
  die("Connection failed: " . $database->connect_error);
}
// Get player list
$sql = "SELECT displayname, level, experience, status  FROM XPerience";
$result = $xpstatsdb->query($sql);

if ($result->num_rows > 0) {
  // output data of each row
  while($row = $result->fetch_assoc()) {
    echo "Player: " . $row["displayname"]. " - Level: " . $row["level"]. " " . $row["experience"]. " - Status: " . $row["status"]. "<br>";
  }
} else {
  echo "0 results";
}
$xpstatsdb->close();
?>
<?php include 'footer.php';?>