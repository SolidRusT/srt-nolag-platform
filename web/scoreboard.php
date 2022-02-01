<?php include 'nav_bar.php';?>
<?php
$nine_dsn = "mysql:host=$db_host;dbname=xpstats_nine";
$eleven_dsn = "mysql:host=$db_host;dbname=xpstats_eleven";
$demo_dsn = "mysql:host=$db_host;dbname=xpstats_demo";
$xpstatsdb_nine = new PDO($nine_dsn, $user, $pass);
$xpstatsdb_nine->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
$xpstatsdb_eleven = new PDO($eleven_dsn, $user, $pass);
$xpstatsdb_eleven->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
$xpstatsdb_demo = new PDO($demo_dsn, $user, $pass);
$xpstatsdb_demo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
?>
<table class="table table-sm">
<thead>
<tr>
  <th>US West 10x</th>
  <th>CA West 100x</th>
  <th>US East 1000x</th>
</tr>
</thead>
<tfoot>
<tr>
  <th>US West 10x</th>
  <th>CA West 100x</th>
  <th>US East 1000x</th>
</tr>
</tfoot>
<tbody>
<tr>
  <td>
    <table class="table table-hover">
    <thead>
    <tr>
    <th>Level &nbsp;&nbsp;</th>
    <th>Players(Top 100)</th>
    <th>XP</th>
    <th>Stats</th>
    <th>Skills</th>
    <th>Status</th>
    </tr>
    </thead>
    <tfoot>
    <tr>
    <th>Level &nbsp;&nbsp;</th>
    <th>Players(Top 100)</th>
    <th>XP</th>
    <th>Stats</th>
    <th>Skills</th>
    <th>Status</th>
    </tr>
    </tfoot>
    <tbody>
    <tr>
    <?php
      $query = $xpstatsdb_nine->query("SELECT * FROM XPerience ORDER BY level DESC limit 0,100");
      while ($row = $query->fetch()) {
      echo "<td align=\"center\">" . $row['level'] . "</td>";
      echo "<td align=\"left\">" . $row['displayname'] ."</td>";
      echo "<td align=\"left\">" . $row['experience'] . "</td>";
      echo "<td align=\"right\">" . $row['statpoint'] . "</td>";
      echo "<td align=\"right\">" . $row['skillpoint'] . "</td>";
      echo "<td align=\"center\">" . $row['Status'] . "</td>";
    }
    ?>
    </tr>
    </tbody>
    </table>
  </td>
</tr>
<tr>
  <td>
  <table class="table table-hover">
    <thead>
    <tr>
    <th>Level &nbsp;&nbsp;</th>
    <th>Players(Top 100)</th>
    <th>XP</th>
    <th>Stats</th>
    <th>Skills</th>
    <th>Status</th>
    </tr>
    </thead>
    <tfoot>
    <tr>
    <th>Level &nbsp;&nbsp;</th>
    <th>Players(Top 100)</th>
    <th>XP</th>
    <th>Stats</th>
    <th>Skills</th>
    <th>Status</th>
    </tr>
    </tfoot>
    <tbody>
    <tr>
  <?php
  $query = $xpstatsdb_eleven->query("SELECT * FROM XPerience ORDER BY level DESC limit 0,100");
  while ($row = $query->fetch()) {
  echo "<td align=\"center\">" . $row['level'] . "</td>";
  echo "<td align=\"left\">" . $row['displayname'] ."</td>";
  echo "<td align=\"left\">" . $row['experience'] . "</td>";
  echo "<td align=\"right\">" . $row['statpoint'] . "</td>";
  echo "<td align=\"right\">" . $row['skillpoint'] . "</td>";
  echo "<td align=\"center\">" . $row['Status'] . "</td>";
}
?>
</tr>
    </tbody>
    </table>
  </td>
</tr>
<tr>
  <td>
  <table class="table table-hover">
    <thead>
    <tr>
    <th>Level &nbsp;&nbsp;</th>
    <th>Players(Top 100)</th>
    <th>XP</th>
    <th>Stats</th>
    <th>Skills</th>
    <th>Status</th>
    </tr>
    </thead>
    <tfoot>
    <tr>
    <th>Level &nbsp;&nbsp;</th>
    <th>Players(Top 100)</th>
    <th>XP</th>
    <th>Stats</th>
    <th>Skills</th>
    <th>Status</th>
    </tr>
    </tfoot>
    <tbody>
    <tr>
<?php
  $query = $xpstatsdb_demo->query("SELECT * FROM XPerience ORDER BY level DESC limit 0,100");
  while ($row = $query->fetch()) {
  echo "<td align=\"center\">" . $row['level'] . "</td>";
  echo "<td align=\"left\">" . $row['displayname'] ."</td>";
  echo "<td align=\"left\">" . $row['experience'] . "</td>";
  echo "<td align=\"right\">" . $row['statpoint'] . "</td>";
  echo "<td align=\"right\">" . $row['skillpoint'] . "</td>";
  echo "<td align=\"center\">" . $row['Status'] . "</td>";
}
?>
</tr>
    </tbody>
    </table>
</td>
</tr>
</tbody>
</table>
<?php include 'footer.php';?>