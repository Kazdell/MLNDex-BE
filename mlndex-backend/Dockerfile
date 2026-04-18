# ── Stage 1: Build ──
FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS final
WORKDIR /app

# Install native dependencies required by OpenCvSharp4 and PaddleOCR
# Ubuntu 22.04 (jammy) natively provides ffmpeg 4.x (libavcodec.so.58, libswscale.so.5, etc.)
# which exactly matches the dependencies of OpenCvSharp4 linux-x64 binaries.
RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    libgdiplus libc6-dev libfontconfig1 \
    libgl1 libglib2.0-0 libgomp1 libsm6 libxext6 libxrender-dev \
    libgtk2.0-0 ffmpeg libavcodec-dev libavformat-dev libswscale-dev libavutil-dev \
    libdc1394-25 libopenexr-dev \
    tesseract-ocr tesseract-ocr-eng tesseract-ocr-vie tesseract-ocr-jpn tesseract-ocr-jpn-vert \
    libtesseract-dev libleptonica-dev \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Fix: OpenCvSharp looks for 'OpenCvSharpExtern.so' but NuGet ships 'libOpenCvSharpExtern.so'
RUN ln -sf /app/runtimes/linux-x64/native/libOpenCvSharpExtern.so /app/runtimes/linux-x64/native/OpenCvSharpExtern.so \
    && ldconfig

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5285
ENV PORT=5285
ENV LD_LIBRARY_PATH=/app/runtimes/linux-x64/native:$LD_LIBRARY_PATH

EXPOSE 5285
ENTRYPOINT ["dotnet", "mlndex-backend.dll"]