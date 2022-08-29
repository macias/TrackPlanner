# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/aspnet:6.0

COPY run_image_stack.sh app/
COPY TrackPlanner.RestService/bin/Release/net6.0/publish app/restservice
COPY TrackPlanner.WebUI.Server/bin/Release/net6.0/publish app/webui

WORKDIR /app
ENTRYPOINT ["./run_image_stack.sh"]