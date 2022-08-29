#!/bin/sh

MAPS=$(realpath  ../maps/)
SCHEDULES=$(realpath  ../schedules/)

docker run \
  -p 8700:8700 \
  -p 5200:5200 \
  -v $MAPS:/maps/ \
  -v $SCHEDULES:/schedules/ \
  --name trackplanner trackplanner