from flask import Flask, render_template
import mysql.connector
import configparser

# Load configuration values
config = configparser.ConfigParser()
config.read('config.ini')

db_config = {
    'host': config.get('database', 'host'),
    'port': int(config.get('database', 'port')),
    'user': config.get('database', 'user'),
    'password': config.get('database', 'password'),
    'database': config.get('database', 'database')
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
    