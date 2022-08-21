<?php
   require 'steamauth/steamauth.php';
   require '../main.conf.php';
   require 'site.conf.php';
   
   if(isset($_GET['steamid'])) {
       echo "logged in";
   }

    if(isset($_GET['action']) && $_GET['action'] == 'logout') {
        session_destroy();
        unset($_SESSION['steamid']);
    }
?>
<!DOCTYPE html>
<html lang="en">
   <head>
      <title><?php echo SiteName; ?> &bull; Link Account</title>
      <!-- Bootstrap core CSS -->
      <link href="vendor/bootstrap/css/bootstrap.min.css" rel="stylesheet">
      <!-- Custom fonts for this template -->
      <link href="vendor/fontawesome-free/css/all.min.css" rel="stylesheet">
      <link href="https://fonts.googleapis.com/css?family=Varela+Round" rel="stylesheet">
      <link href="https://fonts.googleapis.com/css?family=Nunito:200,200i,300,300i,400,400i,600,600i,700,700i,800,800i,900,900i" rel="stylesheet">
      <link href="https://fonts.googleapis.com/css2?family=Roboto:wght@300&display=swap" rel="stylesheet">
      <!-- Custom styles for this template -->
      <link href="css/grayscale.min.css" rel="stylesheet">
      <link href="extra.css" rel="stylesheet">
      <link href="https://stackpath.bootstrapcdn.com/font-awesome/4.7.0/css/font-awesome.min.css" rel="stylesheet" integrity="sha384-wvfXpqpZZVQGK6TAh5PVlGOfQNHSoD2xbE+QkPxCAFlNEevoEH3Sl0sibVcOQVnN" crossorigin="anonymous">
   </head>
   <body id="page-top">
      <!-- Navigation -->
      <nav class="navbar navbar-expand-lg navbar-light fixed-top" id="mainNav">
         <div class="container">
            <a class="navbar-brand js-scroll-trigger" href="#page-top"><?php echo SiteName; ?> Admin Login</a>
            <button class="navbar-toggler navbar-toggler-right" type="button" data-toggle="collapse" data-target="#navbarResponsive" aria-controls="navbarResponsive" aria-expanded="false" aria-label="Toggle navigation">
            Menu
            <i class="fas fa-bars"></i>
            </button>
            <div class="collapse navbar-collapse" id="navbarResponsive">
               <ul class="navbar-nav ml-auto">
                  <li class="nav-item">
                     <a class="nav-link js-scroll-trigger" href="<?php echo Domain; ?>">Home</a>
                  </li>
               </ul>
            </div>
         </div>
      </nav>
      <!-- Header -->
      <header class="masthead">
         <div class="container d-flex h-100 align-items-center">
            <div class="mx-auto text-center">
               <img src='https://i.imgur.com/D5nNAk3.png' height='300px;'>
               <div class="contentz">
                  <?php
                     if(!isset($_SESSION['steamid'])) {
                     
                         echo '<a href="?login" style="font-size:30px; background-color: #444;" class="btn btn-primary test2 js-scroll-trigger"><i class="fa fa-steam" aria-hidden="true"></i> Login with Steam</a>';
                       } else {
                           include ('steamauth/userInfo.php'); 
                     
                           $CheckUser = $database->prepare("SELECT * FROM users WHERE steam_id = ? AND staff_flag = 1");
                           $CheckUser->execute(array($_SESSION['steamid']));
                           $Count = $CheckUser->rowCount();
                     
                           echo '<a href="" style="font-size:30px; background-color: transparent; border: 1px solid #444; margin-left: 10px;" class="btn btn-primary btn-primary-test js-scroll-trigger"><i class="fa fa-steam" aria-hidden="true"></i> <font style="color: lime;"">Logged In</font></a>';
                     
                           if($Count > 0) {
                     
                                 echo "<br /><br /><h5><font color='white'>Hello, " . $_SESSION['steam_personaname'] . "!</font> </h5><br />";
                                 ?>
                                 <h5><font style='color:#ff9100;'>Redirecting Shortly...</font></h5> <?php
                                    header( "refresh:5;url=home.php" );
                                } else {
                                    ?> <br /><br /><h5><font style='color:#ff9100;'>You aren't an admin. Please <a href='?action=logout'>leave.</a></font></h5> <?php
                                } 
                           }
                     ?>
               </div>
            </div>
         </div>
         </div>
      </header>
      <!-- Bootstrap core JavaScript -->
      <script src="../vendor/jquery/jquery.min.js"></script>
      <script src="../vendor/bootstrap/js/bootstrap.bundle.min.js"></script>
      <!-- Plugin JavaScript -->
      <script src="../vendor/jquery-easing/jquery.easing.min.js"></script>
      <!-- Custom scripts for this template -->
      <script src="../js/grayscale.min.js"></script>
   </body>
</html>