<?php include 'nav_bar.php';?>
<?php
// Create connection to XPerience
$dsn = "mysql:host=$db_host;dbname=XPerience";
$xpstatsdb = new PDO($dsn, $user, $pass);
$xpstatsdb->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$query = "SELECT displayname, level, experience, status  FROM XPerience;";
$data = [$displayname,$level,$experience,$status];
//$xpstats->prepare($query)->execute($data);
$xpstats = $xpstatsdb->query($query);
// Get player list
if ($xpstats->num_rows > 0) {
  // output data of each row
  while($row = $xpstats->fetch_assoc()) {
    echo "Player: " . $row["displayname"]. " - Level: " . $row["level"]. " " . $row["experience"]. " - Status: " . $row["status"]. "<br>";
  }
} else {
  echo "0 results";
}
// Close the DB
$xpstatsdb->close();
?>
<?php include 'footer.php';?>