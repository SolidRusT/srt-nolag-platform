<?php include 'nav_bar.php';
$nine_dsn = "mysql:host=$db_host;dbname=RustPlayers";
$RustPlayers_nine = new PDO($nine_dsn, $user, $pass);
$RustPlayers_nine->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
?>
<table class="table table-dark">
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
              $query = $RustPlayers_nine->query("SELECT 'name','steamid','Last Seen','Time Played','First Connection' FROM west ORDER BY level DESC limit 0,100");
              while ($row = $query->fetch()) {
                echo "<tr>";
                echo "<th scope=\"row\" align=\"center\">" . $row['level'] . "</th>";
                echo "<td>" . $row['name'] ."</td>";
                echo "<td>" . $row['Last Seen'] . "</td>";
                echo "<td>" . $row['Time Played'] . "</td>";
                echo "<td>" . $row['First Connection'] . "</td>";
                echo "<td>" . $row['steamid'] . "</td>";
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