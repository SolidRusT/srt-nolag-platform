from flask import Flask, render_template
import mysql.connector

# Add your MySQL credentials here
db_config = {
    'host': '10.10.10.11',
    'port': 3306,
    'user': 'your_username',
    'password': 'your_password',
    'database': 'package_repo'
}

app = Flask(__name__)

@app.route("/")
def index():
    cnx = mysql.connector.connect(**db_config)
    cursor = cnx.cursor()

    query = "SELECT package_name, package_author, package_version, package_description, timestamp FROM packages"
    cursor.execute(query)

    packages = cursor.fetchall()

    cursor.close()
    cnx.close()

    return render_template("index.html", packages=packages)

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=8000)
    