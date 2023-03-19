```tree
.
├── package_browser
│   ├── package_browser.py
│   ├── requirements.txt
│   └── venv
└── package_watcher
    ├── package_watcher.py
    ├── requirements.txt
    └── venv
```

Make sure this SQl is on your database server

```sql
CREATE DATABASE package_repo;
USE package_repo;

CREATE TABLE IF NOT EXISTS packages (
    id INT AUTO_INCREMENT PRIMARY KEY,
    package_name VARCHAR(255) UNIQUE NOT NULL,
    package_author VARCHAR(255) NOT NULL,
    package_version VARCHAR(255) NOT NULL,
    package_description TEXT NOT NULL,
    timestamp TIMESTAMP NOT NULL
);
```

create a user for this app

```sql
CREATE USER 'package_repo'@'%' IDENTIFIED BY 'SomePassword123';
GRANT ALL PRIVILEGES ON *.* TO 'package_repo'@'%';
FLUSH PRIVILEGES;
```

If you are on windows, then for the virtualenv part instead use:
```bash
venv\Scripts\activate
```

To exit the virtualenv

```bash
deactivate
```
