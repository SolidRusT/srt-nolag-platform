<?php include 'nav_bar.php';?>
<?php
// Create connection to XPerience
$dsn = "mysql:host=$db_host;dbname=XPerience";
$xpstatsdb = new PDO($dsn, $user, $pass);
$xpstatsdb->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
// Check connection
if(!empty($displayname)){
  $query = "SELECT displayname, level, experience, status  FROM XPerience;";
  $data = [$displayname,$level,$experience,$status];
  $dbc->prepare($query)->execute($data);
  echo "Thank you. The record has been sent successfully.<br><br>";}
else{
  echo '<h1>Please use the contact form or don\'t leave an empty field!</h1>';
}
// Get player list
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