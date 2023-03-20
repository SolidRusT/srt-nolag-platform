#!/bin/bash
source venv/bin/activate && python package_watcher.py ../packages &
deactivate