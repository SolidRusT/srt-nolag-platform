<?php include 'nav_bar.php';
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
<table class="table table-dark">
  <thead>
    <tr>
      <th>US West 10x</th>
      <th>US East 1000x</th>
    </tr>
  </thead>
  <tfoot>
    <tr>
      <th>US West 10x</th>
      <th>US East 1000x</th>
    </tr>
  </tfoot>
  <tbody>
    <tr>
      <td><table class="table table-light table-striped">
          <thead>
            <tr>
              <th>Level &nbsp;&nbsp;</th>
              <th>Players(Top 100)</th>
              <th>XP</th>
              <th>Stats</th>
              <th>Skills</th>
              <th>Online</th>
            </tr>
          </thead>
          <tfoot>
            <tr>
              <th>Level &nbsp;&nbsp;</th>
              <th>Players(Top 100)</th>
              <th>XP</th>
              <th>Stats</th>
              <th>Skills</th>
              <th>Online</th>
            </tr>
          </tfoot>
          <tbody>
            <?php
              $query = $xpstatsdb_nine->query("SELECT * FROM XPerience ORDER BY level DESC limit 0,100");
              while ($row = $query->fetch()) {
                echo "<tr>";
                echo "<th scope=\"row\" align=\"center\">" . $row['level'] . "</th>";
                echo "<td>" . $row['displayname'] ."</td>";
                echo "<td>" . $row['experience'] . "</td>";
                echo "<td>" . $row['statpoint'] . "</td>";
                echo "<td>" . $row['skillpoint'] . "</td>";
                echo "<td>" . $row['Status'] . "</td>";
                echo "</tr>";
              }
            ?>
          </tbody>
        </table></td>
      <td><table class="table table-light table-striped">
          <thead>
            <tr>
              <th>Level &nbsp;&nbsp;</th>
              <th>Players(Top 100)</th>
              <th>XP</th>
              <th>Stats</th>
              <th>Skills</th>
              <th>Online</th>
            </tr>
          </thead>
          <tfoot>
            <tr>
              <th>Level &nbsp;&nbsp;</th>
              <th>Players(Top 100)</th>
              <th>XP</th>
              <th>Stats</th>
              <th>Skills</th>
              <th>Online</th>
            </tr>
          </tfoot>
          <tbody>
            <?php
              $query = $xpstatsdb_demo->query("SELECT * FROM XPerience ORDER BY level DESC limit 0,100");
              while ($row = $query->fetch()) {
                echo "<tr>";
                echo "<th scope=\"row\" align=\"center\">" . $row['level'] . "</th>";
                echo "<td>" . $row['displayname'] ."</td>";
                echo "<td>" . $row['experience'] . "</td>";
                echo "<td>" . $row['statpoint'] . "</td>";
                echo "<td>" . $row['skillpoint'] . "</td>";
                echo "<td>" . $row['Status'] . "</td>";
                echo "</tr>";
              }
            ?>
          </tbody>
        </table>
      </td>
    </tr>
  </tbody>
</table>
<?php include 'footer.php';?>