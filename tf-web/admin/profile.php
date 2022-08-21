<?php
    //DO NOT TOUCH UNLESS DIFFERENT LOCATION//
    require '../main.conf.php';
    require 'site.conf.php';

    session_start();

    $CheckUser = $database->prepare("SELECT * FROM users WHERE steam_id = ? AND staff_flag = 1");
    $CheckUser->execute(array($_SESSION['steamid']));
    $Count = $CheckUser->rowCount();

    if ($Count < 1) {
        session_destroy();
        header('location: index.php');
        die();
    } else {
        function getNameFromID($steamid){
            $xml = simplexml_load_file("http://steamcommunity.com/profiles/$steamid/?xml=1");//link to user xml
            if(!empty($xml)) {
                $username = $xml->steamID;
                return $username;
            }
        }
    
        function getPicFromID($steamid){
            $xml = simplexml_load_file("http://steamcommunity.com/profiles/$steamid/?xml=1");//link to user xml
            if(!empty($xml)) {
                $pic = $xml->avatarFull;
                return $pic;
            }
        }

        $getUser = $database->prepare("SELECT * FROM users WHERE steam_id = ?");
        $getUser->execute(array($_GET['steam']));
        $user = $getUser->fetch();
        $count = $getUser->rowCount();

        if ($count < 1) {
            header("Refresh:0; url=users.php");
        }

        if(isset($_POST) && isset($_POST['steamid'])) {
            $steam = "";
            $discord = "";
            $flag = "";
    
            if($_POST['steamid'] === $user['steam_id']) {
                $steam = $user['steam_id'];
            } else {
                $steam = $_POST['steamid'];
            }
    
            if($_POST['discordid'] === $user['discord_id']) {
                $discord = $user['discord_id'];
            } else {
                $discord = $_POST['discordid'];
            }
    
            if($_POST['flag'] === $user['staff_flag']) {
                $flag = $user['staff_flag'];
            } else {
                $flag = $_POST['flag'];
            }
    
            try {
                $database->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
                $database->setAttribute(PDO::ATTR_EMULATE_PREPARES, false);
    
                $updateUser = $database->prepare("UPDATE `users` SET `steam_id`=?,`discord_id`=?,`staff_flag`=? WHERE steam_id = ?");
                $updateUser->execute(array($steam, $discord, $flag, $user['steam_id']));
    
                header("Refresh:0");
            }
            catch(Exception $e) {
                echo 'Exception -> ';
                var_dump($e->getMessage());
            }
        }
    }
?>

<!DOCTYPE html>
<html dir="ltr" lang="en">

<head>
    <meta charset="utf-8">
    <meta http-equiv="X-UA-Compatible" content="IE=edge">
    <!-- Tell the browser to be responsive to screen width -->
    <meta name="viewport" content="width=device-width, initial-scale=1">

    <meta name="description" content="<?php echo SiteDescription; ?>">
    <meta property="og:title" content="<?php echo SiteName; ?> &bull; Admin Panel">
    <meta property="og:description" content="<?php echo SiteName; ?> Admin Panel">
    <meta property="og:site_name" content="<?php echo Domain; ?>">
    <meta name="theme-color" content="#ff9100">

    <meta name="robots" content="noindex,nofollow">
    <title><?php echo SiteName; ?> Admin Panel</title>
    <!-- Custom CSS -->
    <link href="plugins/bower_components/chartist/dist/chartist.min.css" rel="stylesheet">
    <link rel="stylesheet" href="plugins/bower_components/chartist-plugin-tooltips/dist/chartist-plugin-tooltip.css">
    <!-- Custom CSS -->
    <link href="css/style.min.css" rel="stylesheet">

    <link href="https://fonts.googleapis.com/css2?family=Bebas+Neue&display=swap" rel="stylesheet">
</head>

<body>
    <!-- ============================================================== -->
    <!-- Preloader - style you can find in spinners.css -->
    <!-- ============================================================== -->
    <div class="preloader">
        <div class="lds-ripple">
            <div class="lds-pos"></div>
            <div class="lds-pos"></div>
        </div>
    </div>
    <!-- ============================================================== -->
    <!-- Main wrapper - style you can find in pages.scss -->
    <!-- ============================================================== -->
    <div id="main-wrapper" data-layout="vertical" data-navbarbg="skin5" data-sidebartype="full"
        data-sidebar-position="absolute" data-header-position="absolute" data-boxed-layout="full">
        <!-- ============================================================== -->
        <!-- Topbar header - style you can find in pages.scss -->
        <!-- ============================================================== -->
        <header class="topbar" data-navbarbg="skin5">
            <nav class="navbar top-navbar navbar-expand-md navbar-dark">
                <div class="navbar-header" data-logobg="skin6">
                    <!-- ============================================================== -->
                    <!-- Logo -->
                    <!-- ============================================================== -->
                    <a class="navbar-brand" href="home.php">
                        <!-- Logo icon -->
                        <b class="logo-icon">
                            <!-- Dark Logo icon -->
                            <img src='admin.png' style="width: 50%; height: 50%"/>​
                        </b>
                        <!--End Logo icon -->
                        <!-- Logo text -->
                        <span class="logo-text">
                            <!-- dark Logo text -->
                            <font color='black' size='6rem' style="font-family: 'Bebas Neue', cursive;"><?php echo SiteName; ?></font>
                        </span>
                    </a>
                    <!-- ============================================================== -->
                    <!-- End Logo -->
                    <!-- ============================================================== -->
                    <!-- ============================================================== -->
                    <!-- toggle and nav items -->
                    <!-- ============================================================== -->
                    <a class="nav-toggler waves-effect waves-light text-dark d-block d-md-none"
                        href="javascript:void(0)"><i class="ti-menu ti-close"></i></a>
                </div>
                <!-- ============================================================== -->
                <!-- End Logo -->
                <!-- ============================================================== -->
                <div class="navbar-collapse collapse" id="navbarSupportedContent" data-navbarbg="skin5">
                    <ul class="navbar-nav d-none d-md-block d-lg-none">
                        <li class="nav-item">
                            <a class="nav-toggler nav-link waves-effect waves-light text-white"
                                href="javascript:void(0)"><i class="ti-menu ti-close"></i></a>
                        </li>
                    </ul>
                    <!-- ============================================================== -->
                    <!-- Right side toggle and nav items -->
                    <!-- ============================================================== -->
                    <ul class="navbar-nav ml-auto d-flex align-items-center">

                        <!-- ============================================================== -->
                        <!-- Search -->
                        <!-- ============================================================== -->
                        <!--<li class=" in">
                            <form role="search" class="app-search d-none d-md-block mr-3">
                                <input type="text" placeholder="Search..." class="form-control mt-0">
                                <a href="" class="active">
                                    <i class="fa fa-search"></i>
                                </a>
                            </form>
                        </li>-->
                        <!-- ============================================================== -->
                        <!-- User profile and search -->
                        <!-- ============================================================== -->
                        <li>
                            <a class="profile-pic" href="#">
                                <img src="<?php echo getPicFromID($user['steam_id']); ?>" alt="user-img" width="36"
                                    class="img-circle"><span class="text-white font-medium"><?php echo getNameFromID($user['steam_id']); ?></span></a>
                        </li>
                        <!-- ============================================================== -->
                        <!-- User profile and search -->
                        <!-- ============================================================== -->
                    </ul>
                </div>
            </nav>
        </header>
        <!-- ============================================================== -->
        <!-- End Topbar header -->
        <!-- ============================================================== -->
        <!-- ============================================================== -->
        <!-- Left Sidebar - style you can find in sidebar.scss  -->
        <!-- ============================================================== -->
        <aside class="left-sidebar" data-sidebarbg="skin6">
            <!-- Sidebar scroll-->
            <div class="scroll-sidebar">
                <!-- Sidebar navigation-->
                <nav class="sidebar-nav">
                    <ul id="sidebarnav">
                        <!-- User Profile-->
                        <li class="sidebar-item pt-2">
                            <a class="sidebar-link waves-effect waves-dark sidebar-link" href="home.php"
                                aria-expanded="false">
                                <i class="far fa-clock" aria-hidden="true"></i>
                                <span class="hide-menu">Dashboard</span>
                            </a>
                        </li>
                        <li class="sidebar-item">
                            <a class="sidebar-link waves-effect waves-dark sidebar-link" href="users.php"
                                aria-expanded="false">
                                <i class="fa fa-user" aria-hidden="true"></i>
                                <span class="hide-menu">Users</span>
                            </a>
                        </li>
                        <li class="sidebar-item">
                            <a class="sidebar-link waves-effect waves-dark sidebar-link" href="users_search.php"
                                aria-expanded="false">
                                <i class="fa fa-user" aria-hidden="true"></i>
                                <span class="hide-menu">Search Users</span>
                            </a>
                        </li>
                    </ul>

                </nav>
                <!-- End Sidebar navigation -->
            </div>
            <!-- End Sidebar scroll-->
        </aside>
        <!-- ============================================================== -->
        <!-- End Left Sidebar - style you can find in sidebar.scss  -->
        <!-- ============================================================== -->
        <!-- ============================================================== -->
        <!-- Page wrapper  -->
        <!-- ============================================================== -->
        <div class="page-wrapper">
            <!-- ============================================================== -->
            <!-- Bread crumb and right sidebar toggle -->
            <!-- ============================================================== -->
            <div class="page-breadcrumb bg-white">
                <div class="row align-items-center">
                    <div class="col-lg-3 col-md-4 col-sm-4 col-xs-12">
                        <h4 class="page-title text-uppercase font-medium font-14">Profile</h4>
                    </div>
                    <div class="col-lg-9 col-sm-8 col-md-8 col-xs-12">
                        <div class="d-md-flex">
                            <ol class="breadcrumb ml-auto">
                                <li><a href="#">Profile</a></li>
                            </ol>
                        </div>
                    </div>
                </div>
                <!-- /.col-lg-12 -->
            </div>
            <!-- ============================================================== -->
            <!-- End Bread crumb and right sidebar toggle -->
            <!-- ============================================================== -->
            <!-- ============================================================== -->
            <!-- Container fluid  -->
            <!-- ============================================================== -->
            <div class="container-fluid">
                <!-- ============================================================== -->
                <!-- Start Page Content -->
                <!-- ============================================================== -->
                <!-- Row -->
                <div class="row">
                    <!-- Column -->
                    <div class="col-lg-4 col-xlg-3 col-md-12">
                        <div class="white-box">
                            <div class="user-bg"> <img width="100%" alt="user" src="plugins/images/large/img1.jpg">
                                <div class="overlay-box">
                                    <div class="user-content">
                                        <a href="javascript:void(0)"><img src="<?php echo getPicFromID($_GET['steam']); ?>"
                                                class="thumb-lg img-circle" alt="img"></a>
                                        <h4 class="text-white mt-2"><?php echo getNameFromID($_GET['steam']); ?></h4>
                                        <h5 class="text-white mt-2"><?php echo $_GET['steam']; ?></h5>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                    <!-- Column -->
                    <!-- Column -->


                    <div class="col-lg-8 col-xlg-9 col-md-12">
                        <div class="card">
                            <div class="card-body">
                                <form class="form-horizontal form-material" name='updateUser' method='POST'>
                                    <div class="form-group mb-4">
                                        <label class="col-md-12 p-0"><?php echo getNameFromID($_GET['steam']); ?></label>
                                    </div>
                                    <div class="form-group mb-4">
                                        <label for="example-email" class="col-md-12 p-0">Steam ID</label>
                                        <div class="col-md-12 border-bottom p-0">
                                            <input type="text" value="<?php echo $user['steam_id']; ?>"
                                                class="form-control p-0 border-0" name="steamid"
                                                id="example-email">
                                        </div>
                                    </div>
                                    <div class="form-group mb-4">
                                        <label class="col-md-12 p-0">Discord ID</label>
                                        <div class="col-md-12 border-bottom p-0">
                                            <input type="text" name="discordid" value="<?php echo $user['discord_id']; ?>" class="form-control p-0 border-0">
                                        </div>
                                    </div>
                                    <div class="form-group mb-4">
                                        <label class="col-md-12 p-0">Staff Flag</label>
                                        <div class="col-md-12 border-bottom p-0">
                                            <input type="text" name="flag" value="<?php echo $user['staff_flag']; ?>"
                                                class="form-control p-0 border-0">
                                        </div>
                                    </div>

                                    <div class="col-sm-12">
                                            <button class="btn btn-success">Update Profile</button>
                                    </div>
                                </form>
                            </div>
                        </div>
                    </div>
                    <!-- Column -->
                </div>
                <!-- Row -->
                <!-- ============================================================== -->
                <!-- End PAge Content -->
                <!-- ============================================================== -->
                <!-- ============================================================== -->
                <!-- Right sidebar -->
                <!-- ============================================================== -->
                <!-- .right-sidebar -->
                <!-- ============================================================== -->
                <!-- End Right sidebar -->
                <!-- ============================================================== -->
            </div>
            <!-- ============================================================== -->
            <!-- End Container fluid  -->
            <!-- ============================================================== -->
            <!-- ============================================================== -->
            <!-- footer -->
            <!-- ============================================================== -->
            <footer class="footer text-center"> 2020 © <?php echo SiteName; ?><br /> Template @ <a
                    href="https://www.wrappixel.com/">wrappixel.com</a>
            </footer>
            <!-- ============================================================== -->
            <!-- End footer -->
            <!-- ============================================================== -->
        </div>
        <!-- ============================================================== -->
        <!-- End Page wrapper  -->
        <!-- ============================================================== -->
    </div>
    <!-- ============================================================== -->
    <!-- End Wrapper -->
    <!-- ============================================================== -->
    <!-- ============================================================== -->
    <!-- All Jquery -->
    <!-- ============================================================== -->
    <script src="plugins/bower_components/jquery/dist/jquery.min.js"></script>
    <!-- Bootstrap tether Core JavaScript -->
    <script src="plugins/bower_components/popper.js/dist/umd/popper.min.js"></script>
    <script src="bootstrap/dist/js/bootstrap.min.js"></script>
    <script src="js/app-style-switcher.js"></script>
    <script src="plugins/bower_components/jquery-sparkline/jquery.sparkline.min.js"></script>
    <!--Wave Effects -->
    <script src="js/waves.js"></script>
    <!--Menu sidebar -->
    <script src="js/sidebarmenu.js"></script>
    <!--Custom JavaScript -->
    <script src="js/custom.js"></script>
    <!--This page JavaScript -->
    <!--chartis chart-->
    <script src="plugins/bower_components/chartist/dist/chartist.min.js"></script>
    <script src="plugins/bower_components/chartist-plugin-tooltips/dist/chartist-plugin-tooltip.min.js"></script>
    <script src="js/pages/dashboards/dashboard1.js"></script>

    <script>
        function filterTable(event) {
        var filter = event.target.value.toUpperCase();
        var rows = document.querySelector("#myTable").rows;
        
        for (var i = 0; i < rows.length; i++) {
            var firstCol = rows[i].cells[0].textContent.toUpperCase();
            var secondCol = rows[i].cells[1].textContent.toUpperCase();
            var thirdCol = rows[i].cells[2].textContent.toUpperCase();
            var fourthCol = rows[i].cells[3].textContent.toUpperCase();
            if (firstCol.indexOf(filter) > -1 || secondCol.indexOf(filter) > -1 || thirdCol.indexOf(filter) > -1 || fourthCol.indexOf(filter) > -1) {
                rows[i].style.display = "";
            } else {
                rows[i].style.display = "none";
            }      
        }
    }

    document.querySelector('#myInput').addEventListener('keyup', filterTable, false);
</script>
</body>

</html>