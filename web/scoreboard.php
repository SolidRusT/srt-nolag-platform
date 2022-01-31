<?php include 'nav_bar.php';?>
<?php
// Create connection to XPerience
$db = "XPerience"
$conn = new mysqli($db_host, $user, $pass, $db);
// Check connection
if ($conn->connect_error) {
  die("Connection failed: " . $conn->connect_error);
}
// Get player list
$sql = "SELECT displayname, level, experience, status  FROM XPerience";
$result = $conn->query($sql);

if ($result->num_rows > 0) {
  // output data of each row
  while($row = $result->fetch_assoc()) {
    echo "Player: " . $row["displayname"]. " - Level: " . $row["level"]. " " . $row["experience"]. " - Status: " . $row["status"]. "<br>";
  }
} else {
  echo "0 results";
}
$conn->close();
?>
<?php include 'footer.php';?>