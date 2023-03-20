from flask import Flask, request, jsonify
from flask import render_template
from flask_sqlalchemy import SQLAlchemy
import configparser

config = configparser.ConfigParser()
config.read("config.ini")

app = Flask(__name__)
app.config["SQLALCHEMY_DATABASE_URI"] = f"mysql://{config.get('mysql', 'user')}:{config.get('mysql', 'password')}@{config.get('mysql', 'host')}:{config.get('mysql', 'port')}/{config.get('mysql', 'database')}"
app.config["SQLALCHEMY_TRACK_MODIFICATIONS"] = False

db = SQLAlchemy(app)

class ConfigValues(db.Model):
    id = db.Column(db.Integer, primary_key=True)
    server_name = db.Column(db.String(50), nullable=False)
    key_name = db.Column(db.String(50), nullable=False)
    value = db.Column(db.String(255), nullable=False)

@app.route("/api/config_values", methods=["GET", "POST"])
def manage_config_values():
    if request.method == "GET":
        server_name = request.args.get("server_name")
        if server_name:
            config_values = ConfigValues.query.filter_by(server_name=server_name).all()
        else:
            config_values = ConfigValues.query.all()
        return jsonify([cv.to_dict() for cv in config_values])

    elif request.method == "POST":
        data = request.get_json()
        new_config_value = ConfigValues(server_name=data["server_name"], key_name=data["key_name"], value=data["value"])
        db.session.add(new_config_value)
        db.session.commit()
        return jsonify(new_config_value.to_dict()), 201
@app.route("/")
def home():
    return render_template("index.html")

if __name__ == "__main__":
    app.run()
