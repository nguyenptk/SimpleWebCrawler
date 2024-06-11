# Stage 1: Build the .NET application
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build

# Set the working directory
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out

# Stage 2: Use a smaller image with necessary runtime and Chromium
FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS runtime

# Install dependencies and Chromium
RUN apt-get update && apt-get install -y \
    wget gnupg \
    libx11-xcb1 libxcomposite1 libxcursor1 libxdamage1 libxi6 libxtst6 \
    libnss3 libxrandr2 libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 libgbm1 \
    libasound2 libpangocairo-1.0-0 libpango-1.0-0 libx11-6 libxcb1 libxext6 \
    libxfixes3 libepoxy0 libgtk-3-0 xvfb \
    chromium

# Set working directory
WORKDIR /app

# Init out folder first
RUN mkdir -p out

# Copy the build output from the first stage
COPY --from=build /app/out ./

# Set environment variable for PuppeteerSharp to use Chromium
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium

# Run the app
ENTRYPOINT ["dotnet", "WebCrawler.dll"]
