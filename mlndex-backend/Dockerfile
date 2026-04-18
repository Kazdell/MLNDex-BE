# syntax=docker/dockerfile:1.4
# ── Stage 1: Build ──
FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
WORKDIR /src

# Copy project files first for layer caching
COPY ["mlndex-backend/mlndex-backend.csproj", "mlndex-backend/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]

# Cache mount for NuGet packages — persists across --no-cache rebuilds
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "mlndex-backend/mlndex-backend.csproj"

# Copy full source
COPY . .
WORKDIR "/src/mlndex-backend"
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "mlndex-backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Stage 2: Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS final
WORKDIR /app

# Install native dependencies required by OpenCvSharp4 and PaddleOCR
# Ubuntu 22.04 (jammy) natively provides ffmpeg 4.x (libavcodec.so.58, libswscale.so.5, etc.)
# which exactly matches the dependencies of OpenCvSharp4 linux-x64 binaries.
# Cache mount for apt — persists across --no-cache rebuilds
RUN --mount=type=cache,target=/var/cache/apt,sharing=locked \
    --mount=type=cache,target=/var/lib/apt,sharing=locked \
    apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    libgdiplus libc6-dev libfontconfig1 \
    libgl1 libglib2.0-0 libgomp1 libsm6 libxext6 libxrender-dev \
    libgtk2.0-0 ffmpeg libavcodec-dev libavformat-dev libswscale-dev libavutil-dev \
    libdc1394-25 libopenexr-dev \
    tesseract-ocr tesseract-ocr-eng tesseract-ocr-vie tesseract-ocr-jpn tesseract-ocr-jpn-vert \
    libtesseract-dev libleptonica-dev

COPY --from=build /app/publish .

# Fix: OpenCvSharp looks for 'OpenCvSharpExtern.so' but NuGet ships 'libOpenCvSharpExtern.so'
RUN ln -sf /app/runtimes/linux-x64/native/libOpenCvSharpExtern.so /app/runtimes/linux-x64/native/OpenCvSharpExtern.so

# Fix: InteropDotNet.LibraryLoader (Tesseract.NET) searches /app/x64/*.so on Linux x64.
# The LD_LIBRARY_PATH must also include /app/x64 so dlopen() can resolve by name.
# We copy the actual ELF binaries (not symlinks) with exact names expected by InteropDotNet.
RUN cp /usr/lib/x86_64-linux-gnu/liblept.so.5.0.4 /app/x64/libleptonica-1.82.0.so && \
    cp /usr/lib/x86_64-linux-gnu/libtesseract.so.4.0.1 /app/x64/libtesseract50.so

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5285
ENV PORT=5285
ENV LD_LIBRARY_PATH=/app/x64:/app/runtimes/linux-x64/native:/usr/lib/x86_64-linux-gnu

EXPOSE 5285
ENTRYPOINT ["dotnet", "mlndex-backend.dll"]