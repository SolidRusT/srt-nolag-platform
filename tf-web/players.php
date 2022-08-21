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
              <th>Rank</th>
              <th>Player</th>
              <th>Days:Hours:Minutes</th>
              <th>Last Seen</th>
              <th>First Seen</th>
            </tr>
          </thead>
          <tfoot>
            <tr>
              <th>Rank</th>
              <th>Player</th>
              <th>Days:Hours:Minutes</th>
              <th>Last Seen</th>
              <th>First Seen</th>
            </tr>
          </tfoot>
          <tbody>
            <?php
              $query = $RustPlayers_nine->query("SELECT *,@a := @a + 1 serial,SEC_TO_TIME(`Time Played`),FROM_UNIXTIME(`Last Seen`),FROM_UNIXTIME(`First Connection`)
              FROM RustPlayers.west, (SELECT @a :=0) AS dummy
              WHERE `Time Played` IS NOT NULL
              AND name NOT IN ('Suparious','joe_3451','ParmyJack','SolidRusT')
              ORDER BY SEC_TO_TIME(`Time Played`)
              DESC limit 0,100;");
              while ($row = $query->fetch()) {
                echo "<tr>";
                echo "<th scope=\"row\" align=\"center\">" . $row['serial'] . "</th>";
                echo "<td><a href=\"https://steamcommunity.com/profiles/" . $row['steamid'] . "\">" . $row['name'] . "</a></td>";
                echo "<td>" . $row['SEC_TO_TIME(`Time Played`)'] . "</td>";
                echo "<td>" . $row['FROM_UNIXTIME(`Last Seen`)'] . "</td>";
                echo "<td>" . $row['FROM_UNIXTIME(`First Connection`)'] . "</td>";
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