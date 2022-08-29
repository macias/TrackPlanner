#!/bin/sh

dotnet publish -c Release
docker build -t trackplanner .