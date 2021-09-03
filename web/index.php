<?php include('nav_bar.php'); ?>
<div class="container-fluid" style="margin-top:15px">
	<div class="row">
    	<div class="col-md-4"><?php
if (!isset($_SESSION['steamid'])) {
	echo "<p class=\"bg-warning\">&nbsp; Welcome, Guest. Please <a href=\"/link\">login</a> to access more SRT content.</p>";
} else {
	include 'steamauth/userInfo.php';
	$avatar = $steamprofile['avatar'];
    $profile_name = $steamprofile['personaname'];
    echo "<p class=\"bg-success\">Welcome, <a href=\"/profile\">$profile_name</a>!</p>";
}?></div>
	</div>
</div>

<div class="container-fluid">
<div class="row">
<div class="col-sm-5">
  <div class="card">
	<div class="card-body">
    <img src="/images/SoldRust_Header_nine.png" class="card-img-top" alt="connect Nine.SolidRusT.net:28015">
	  <h4 class="card-title">SRT 5x NoBP - Solo/Duo/Trio</h4>
	  <a href="steam://connect/nine.solidrust.net:28015" class="btn btn-primary">connect nine.solidrust.net:28015</a>
	  <p class="card-text"><h6>This server is a fast paced 5x with custom mods.</h6>
		<ul>
		  <li>Map Wipe every MONDAY 10AM-PST</li>
		  <li>Full wipe on the first THURSDAY of the month</li>
		  <li>Max group size of 10 players, including alliances</li>
		  <li>70min Days, 10min Brighter Nights</li>
		  <li>Kits, Skins, Spawn Mini/Sedan/Recycler</li>
		  <li>Craft your own recycler, </li>
		  <li>Raidable Bases / Roaming NPC Raiders</li>
		  <li>5x Gather, Improved stack sizes</li>
		  <li>No Blueprints, larger workbench area</li>
		  <li>Supply Drops, airdrops and custom NPC events</li>
		  <li>Custom Loot tables, Recycle any item for useful mats</li>
		  <li>Additional monument puzzles and utilities</li>
		  <li>5 additional custom radio stations (live DJs)</li>
		  <li>Realistic weather, health effects, bullet impact and explosions</li>
		</ul>
	  </p>
	  <script type="application/javascript">window.addEventListener('message',function(e){if(e.data.uid&&e.data.type==='sizeUpdate'){var i = document.querySelector('iframe[name="'+e.data.uid+'"]');i.style.width = e.data.payload.width;i.style.height = e.data.payload.height;}});</script><iframe src="https://cdn.battlemetrics.com/b/standardVertical/11593491.html?_token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6InN0YW5kYXJkVmVydGljYWwiLCJzZXJ2ZXIiOjExNTkzNDkxLCJvcHRpb25zIjp7ImZvcmVncm91bmQiOiIjODUwMDAwIiwibGlua0NvbG9yIjoiIzAwMDAwMCIsImxpbmVzIjoiIzg1MDAwMCIsImJhY2tncm91bmQiOiIjZmZmZmZmIiwiY2hhcnQiOiJ0aW1lOjFNIiwiY2hhcnRDb2xvciI6IiMwMDEzYWMiLCJtYXhQbGF5ZXJzSGVpZ2h0IjoiMzAwIn0sImxpdmVVcGRhdGVzIjp0cnVlLCJ1c2VyX2lkIjozNTgyMjAsImlhdCI6MTYyODY1Nzc2Nn0.yHhEQSkhA8kijr2RAtkj_vaifFV4_cUCcDVjkyCEDl8" frameborder=0 style="border:0" name="ilqin"></iframe><br>
		<script src="https://rust-servers.net/embed.js?id=162953&type=votes"></script>
      <p class="card-text"><small class="text-muted">Last updated less than 1 min ago</small></p>
	</div>
  </div>
</div>
</div>
</div>
<?php include('footer.php'); ?>