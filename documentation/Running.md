## Running

### Tile server

The first step is setting up some tile server. I use Mapnik and there is an excellent [documentation](https://github.com/Overv/openstreetmap-tile-server/blob/master/README.md)
how to do it easy way using docker.

I ended up with few scripts.

**import-mapnik.sh**
```
#!/bin/sh

if [ $# -eq 0 ]
then
  echo No osm.pbf filepath given
  exit
fi

# path has to be absolute, so we need to convert it
FN=$(realpath $1)

docker run -v $FN:/data/region.osm.pbf \
  -v /bigdata/mapnik/osm-data:/data/database/ \
  -v /bigdata/mapnik/osm-tiles:/data/tiles/ \
  --name running-mapnik overv/openstreetmap-tile-server import
```

Running it is the first step and I recommend running it once before each season (please note it can be lengthy process). Please customize it according to your system.

**run-mapnik.sh**
```
#!/bin/sh

docker run \
  --restart unless-stopped \
  -p 8600:80 \
  --shm-size="192m" \
  -v /bigdata/mapnik/osm-data:/data/database/ \
  -v /bigdata/mapnik/osm-tiles:/data/tiles/ \
  --name running-mapnik overv/openstreetmap-tile-server run
```

Execute it once and it will run forever (as long as docker is running).

### Track planner

There is [docker_run.sh](../app/docker_run.sh) script provided for running the program, again please customize the "maps" and "schedules" paths according to your system.

The first run can be very time consuming because program converts OSM map to its own format. Subsequent executions are somewhat faster but even for moderate regions
like Poland the loading is still slow.

If everything went well, you should be able to access "localhost:5200" address using any web browser and start your planning.

*Please note that while the maps directory binding for docker image are for maps in general, the maps subdirectory within the settings currently point out to the
subdirectory with single OSM map file.*