<?php include 'nav_bar.php';?>
<?php
// Create connection to XPerience
$db = "XPerience"
$user = "srt_sl_lcy";
$pass = "lcy_402";
$db_host = "data.solidrust.net";
$details = "mysql:dbname=$db;host=$db_host";
$database = new PDO($details, $user, $pass);
// Check connection
if ($database->connect_error) {
  die("Connection failed: " . $database->connect_error);
}
// Get player list
$sql = "SELECT displayname, level, experience, status  FROM XPerience";
$result = $database->query($sql);

if ($result->num_rows > 0) {
  // output data of each row
  while($row = $result->fetch_assoc()) {
    echo "Player: " . $row["displayname"]. " - Level: " . $row["level"]. " " . $row["experience"]. " - Status: " . $row["status"]. "<br>";
  }
} else {
  echo "0 results";
}
$database->close();
?>
<?php include 'footer.php';?>