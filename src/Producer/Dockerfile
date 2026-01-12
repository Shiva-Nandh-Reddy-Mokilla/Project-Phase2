# Dockerfile for Producer app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/Producer/Producer.csproj", "src/Producer/"]
RUN dotnet restore "src/Producer/Producer.csproj"

# Copy source code
COPY . .
WORKDIR "/src/src/Producer"
RUN dotnet build "Producer.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Producer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Run the producer
ENTRYPOINT ["dotnet", "Producer.dll"]

