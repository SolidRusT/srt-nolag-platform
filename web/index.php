<?php include('nav_bar.php'); ?>
<div class="container-fluid" style="margin-top:15px">
	<div class="row">
    	<div class="col-md-4"><?php
if (!isset($_SESSION['steamid'])) {
	echo "<p class=\"bg-warning\">Welcome, Guest. Please <a href=\"/link\">login</a> to access more SRT content.</p>";
} else {
	include 'steamauth/userInfo.php';
	$avatar = $steamprofile['avatar'];
    $profile_name = $steamprofile['personaname'];
    echo "<p class=\"bg-success\">Welcome, <a href=\"/profile\">$profile_name</a>!</p>";
}?></div>
	</div>
</div>

<div class="container-fluid">
<div class="card-group">
  <div class="card">
    <img src="/images/SoldRust_Header_two.png" class="card-img-top" alt="connect Two.SolidRusT.net:28015">
    <div class="card-body">
      <h5 class="card-title">SRT Main - Community</h5>
      <p class="card-text">This is a minimalistic version of the Core SolidRusT build. Aavailable as a un-modded Community server.</p>
		<script type="application/javascript">window.addEventListener('message',function(e){if(e.data.uid&&e.data.type==='sizeUpdate'){var i = document.querySelector('iframe[name="'+e.data.uid+'"]');i.style.width = e.data.payload.width;i.style.height = e.data.payload.height;}});</script><iframe src="https://cdn.battlemetrics.com/b/standardVertical/12262212.html?_token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6InN0YW5kYXJkVmVydGljYWwiLCJzZXJ2ZXIiOjEyMjYyMjEyLCJvcHRpb25zIjp7ImZvcmVncm91bmQiOiIjODUwMDAwIiwibGlua0NvbG9yIjoiIzAwMDAwMCIsImxpbmVzIjoiIzg1MDAwMCIsImJhY2tncm91bmQiOiIjZmZmZmZmIiwiY2hhcnQiOiJ0aW1lOjNNIiwiY2hhcnRDb2xvciI6IiMwMDEzYWMiLCJtYXhQbGF5ZXJzSGVpZ2h0IjoiMzAwIn0sImxpdmVVcGRhdGVzIjp0cnVlLCJ1c2VyX2lkIjozNTgyMjAsImlhdCI6MTYyODY1NzY0Mn0.SB-acqava0m9BU2SKQORx9v-Js_o68oAdDB_lw3jFqg" frameborder=0 style="border:0" name="zhxsl"></iframe>
      <p class="card-text"><small class="text-muted">Last updated 2 min ago</small></p>
	  <a href="steam://connect/two.solidrust.net:28015" class="btn btn-primary">connect two.solidrust.net:28015</a>
    </div>
  </div>
  <div class="card">
    <img src="/images/SoldRust_Header_nine.png" class="card-img-top" alt="connect Nine.SolidRusT.net:28015">
    <div class="card-body">
      <h5 class="card-title">SRT 5x NoBP Solo/Duo/Trio</h5>
      <p class="card-text">A fast-paced modded server, with a focus on quality of life and combat training.</p>
	  <script type="application/javascript">window.addEventListener('message',function(e){if(e.data.uid&&e.data.type==='sizeUpdate'){var i = document.querySelector('iframe[name="'+e.data.uid+'"]');i.style.width = e.data.payload.width;i.style.height = e.data.payload.height;}});</script><iframe src="https://cdn.battlemetrics.com/b/standardVertical/11593491.html?_token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6InN0YW5kYXJkVmVydGljYWwiLCJzZXJ2ZXIiOjExNTkzNDkxLCJvcHRpb25zIjp7ImZvcmVncm91bmQiOiIjODUwMDAwIiwibGlua0NvbG9yIjoiIzAwMDAwMCIsImxpbmVzIjoiIzg1MDAwMCIsImJhY2tncm91bmQiOiIjZmZmZmZmIiwiY2hhcnQiOiJ0aW1lOjFNIiwiY2hhcnRDb2xvciI6IiMwMDEzYWMiLCJtYXhQbGF5ZXJzSGVpZ2h0IjoiMzAwIn0sImxpdmVVcGRhdGVzIjp0cnVlLCJ1c2VyX2lkIjozNTgyMjAsImlhdCI6MTYyODY1Nzc2Nn0.yHhEQSkhA8kijr2RAtkj_vaifFV4_cUCcDVjkyCEDl8" frameborder=0 style="border:0" name="ilqin"></iframe>
      <p class="card-text"><small class="text-muted">Last updated less than 1 min ago</small></p>
	  <a href="steam://connect/nine.solidrust.net:28015" class="btn btn-primary">connect nine.solidrust.net:28015</a>
    </div>
  </div>
  <div class="card">
    <img src="/images/SoldRust_Header_one.png" class="card-img-top" alt="connect One.SolidRusT.net:28015">
    <div class="card-body">
      <h5 class="card-title">SRT Demo - Testing Server</h5>
      <p class="card-text">A low key test server, that run the latest experimental builds of SolidRusT SRT.</p>
		<script type="application/javascript">window.addEventListener('message',function(e){if(e.data.uid&&e.data.type==='sizeUpdate'){var i = document.querySelector('iframe[name="'+e.data.uid+'"]');i.style.width = e.data.payload.width;i.style.height = e.data.payload.height;}});</script><iframe src="https://cdn.battlemetrics.com/b/standardVertical/12392883.html?_token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6InN0YW5kYXJkVmVydGljYWwiLCJzZXJ2ZXIiOjEyMzkyODgzLCJvcHRpb25zIjp7ImZvcmVncm91bmQiOiIjODUwMDAwIiwibGlua0NvbG9yIjoiIzAwMDAwMCIsImxpbmVzIjoiIzg1MDAwMCIsImJhY2tncm91bmQiOiIjZmZmZmZmIiwiY2hhcnQiOiJ1bmlxdWU6MU0iLCJjaGFydENvbG9yIjoiIzAwMTNhYyIsIm1heFBsYXllcnNIZWlnaHQiOiIzMDAifSwibGl2ZVVwZGF0ZXMiOnRydWUsInVzZXJfaWQiOjM1ODIyMCwiaWF0IjoxNjI4NjU3OTA3fQ.pLARp_vMmID8Psv40bDec8lB3WilmL-QKn5mUfBOvvg" frameborder=0 style="border:0" name="qujxg"></iframe>
      <p class="card-text"><small class="text-muted">Last updated 1 min ago</small></p>
	  <a href="steam://connect/one.solidrust.net:28015" class="btn btn-primary">connect one.solidrust.net:28015</a>
    </div>
  </div>
</div>
</div>

<?php include('footer.php'); ?>