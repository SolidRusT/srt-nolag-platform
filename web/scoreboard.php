<?php include 'nav_bar.php';?>
<?php
// Create connection to XPerience
$dsn = "mysql:host=$db_host;dbname=XPerience";
$xpstatsdb = new PDO($dsn, $user, $pass);
$xpstatsdb->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
// Get player data
$query = $xpstatsdb->query("SELECT * FROM XPerience ORDER BY level DESC limit 0,100");
// print a nice table
echo "<table style=\"background-color:#FFFFE0;\" border = '2'>
  <tr style=\"background-color:#BDB76B;color:#ffffff;\">
  <th>Level</th>
  <th>Players(Top 100)</th>
  <th>XP</th>
  <th>Stats</th>
  <th>Skills</th>
  <th>Status</th>
  </tr>";
while ($row = $query->fetch()) {
  echo "<tr>";
  echo "<td align=\"right\">" . $row['level'] . "&nbsp;&nbsp;</td>";
  echo "<td align=\"left\">" . $row['displayname'] ."</td>";
  echo "<td align=\"left\">" . $row['experience'] . "</td>";
  echo "<td align=\"right\">" . $row['statpoint'] . "</td>";
  echo "<td align=\"right\">" . $row['skillpoint'] . "</td>";
  echo "<td align=\"center\">" . $row['Status'] . "</td>";
  echo "</tr>";
}
echo "</table>";
// show sql version
$sqlver = $xpstatsdb->query("SELECT VERSION()");
$version = $sqlver->fetch();
echo $version[0] . PHP_EOL;
?>
<?php include 'footer.php';?>
