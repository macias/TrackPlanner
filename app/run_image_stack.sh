#!/bin/sh

cd /app/restservice ; ./TrackPlanner.RestService &
cd /app/webui ; ./TrackPlanner.WebUI.Server &

wait 

