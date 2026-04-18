# ── Stage 1: Build ──
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for layer caching
COPY ["mlndex-backend/mlndex-backend.csproj", "mlndex-backend/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "mlndex-backend/mlndex-backend.csproj"

# Copy full source
COPY . .
WORKDIR "/src/mlndex-backend"
RUN dotnet publish "mlndex-backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Stage 2: Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Use Singapore mirror (closer to VN) for faster apt-get
RUN sed -i 's|http://deb.debian.org|http://mirror.sg.gs|g' /etc/apt/sources.list.d/debian.sources 2>/dev/null; \
    sed -i 's|http://deb.debian.org|http://mirror.sg.gs|g' /etc/apt/sources.list 2>/dev/null; \
    apt-get update && apt-get install -y --no-install-recommends \
    libgdiplus libc6-dev libfontconfig1 \
    libgl1 libglib2.0-0 libgomp1 libsm6 libxext6 libxrender-dev \
    tesseract-ocr tesseract-ocr-eng tesseract-ocr-vie tesseract-ocr-jpn tesseract-ocr-jpn-vert \
    libtesseract-dev libleptonica-dev \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5285
ENV PORT=5285

EXPOSE 5285
ENTRYPOINT ["dotnet", "mlndex-backend.dll"]