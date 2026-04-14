FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy entire solution context
COPY . .

# Restore packages from solution
RUN dotnet restore "MLNDex-BE.sln"

# Publish main project
RUN dotnet publish "mlndex-backend/mlndex-backend.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5285
ENTRYPOINT ["dotnet", "mlndex-backend.dll"]