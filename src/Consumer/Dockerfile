# Build stage - compiles the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY src/Consumer/Consumer.csproj ./Consumer/
RUN dotnet restore ./Consumer/Consumer.csproj

# Copy source code and build
COPY src/Consumer/ ./Consumer/
RUN dotnet build ./Consumer/Consumer.csproj -c Release -o /app/build
RUN dotnet publish ./Consumer/Consumer.csproj -c Release -o /app/publish

# Runtime stage - runs the app
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN useradd -m -u 1000 appuser && chown -R appuser:appuser /app
USER appuser

# Copy the built app from build stage
COPY --from=build /app/publish .

# Run the consumer app
ENTRYPOINT ["dotnet", "Consumer.dll"]
