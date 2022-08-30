#!/bin/sh

CONFIG=$(realpath  ../config/)
MAPS=$(realpath  ../maps/)
SCHEDULES=$(realpath  ../schedules/)

docker run \
  -p 8700:8700 \
  -p 5200:5200 \
  -v $CONFIG:/app/config/ \
  -v $MAPS:/maps/ \
  -v $SCHEDULES:/schedules/ \
  --name trackplanner xmacias/trackplanner