<?php include 'nav_bar.php';?>
<?php
// Create connection to XPerience
$dsn = "mysql:host=$db_host;dbname=XPerience";
$xpstatsdb = new PDO($dsn, $user, $pass);
$xpstatsdb->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
// Get player data
$query = $xpstatsdb->query("SELECT * FROM XPerience ORDER BY level DESC limit 0,100");
// print a nice table
echo "<table border = '2'>
  <tr>
  <th>Level</th>
  <th>Player</th>
  <th>XP</th>
  <th>Status</th>
  </tr>";
while ($row = $query->fetch()) {
  echo "<tr>";
  echo "<td>" . $row['level'] . "</td>";
  echo "<td>" . $row['displayname'] ."</td>";
  echo "<td>" . $row['experience'] . "</td>";
  echo "<td>" . $row['status'] . "</td>";
  echo "</tr>";
}
echo "</table>";
// show sql version
$sqlver = $xpstatsdb->query("SELECT VERSION()");
$version = $sqlver->fetch();
echo $version[0] . PHP_EOL;
?>
<?php include 'footer.php';?>
