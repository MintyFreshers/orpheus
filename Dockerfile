# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Orpheus.csproj", "."]
RUN dotnet restore "./Orpheus.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./Orpheus.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Orpheus.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app

# Switch to root to install python3, ffmpeg, and latest yt-dlp via pip
USER root
RUN apt-get update 
RUN apt-get install -y python3 python3-pip ffmpeg python3-venv libopus0
RUN cp /usr/lib/x86_64-linux-gnu/libopus.so.0.8.0 /usr/lib/x86_64-linux-gnu/libopus.so
RUN apt-get clean 
RUN rm -rf /var/lib/apt/lists/*

# Create a virtual environment for yt-dlp
RUN python3 -m venv /opt/yt-dlp-venv

# Install yt-dlp into the virtual environment using its specific pip
RUN /opt/yt-dlp-venv/bin/pip install -U "yt-dlp[default]"

# Add the virtual environment's bin directory to the PATH
# This makes yt-dlp accessible to the application run by the ENTRYPOINT
ENV PATH="/opt/yt-dlp-venv/bin:$PATH"

# Switch back to app user
USER $APP_UID

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Orpheus.dll"]

# Document usage of the DISCORD_TOKEN environment variable
# When running the container, set the token like this:
# docker build --no-cache -t orpheus .^C
# docker run -e DISCORD_TOKEN=your_token_here orhpeus