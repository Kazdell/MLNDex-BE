param (
    [string]$TargetDir = ".\tessdata"
)

# Thư mục sao lưu data cũ
$BackupDir = ".\tessdata_backup"

if (Test-Path $TargetDir) {
    if (-not (Test-Path $BackupDir)) {
        New-Item -ItemType Directory -Path $BackupDir | Out-Null
    }
    Write-Host "Backing up old tessdata to $BackupDir..."
    Move-Item -Path "$TargetDir\*" -Destination $BackupDir -Force -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $TargetDir | Out-Null
}

$Langs = @(
    "eng",
    "vie",
    "jpn",
    "jpn_vert",
    "chi_sim",
    "chi_sim_vert",
    "chi_tra",
    "chi_tra_vert",
    "kor",
    "kor_vert",
    "osd"
)

$BaseUrl = "https://github.com/tesseract-ocr/tessdata_best/raw/main"

foreach ($lang in $Langs) {
    $FileName = "$lang.traineddata"
    $Url = "$BaseUrl/$FileName"
    $DestPath = Join-Path $TargetDir $FileName
    
    Write-Host "Downloading $FileName..."
    try {
        Invoke-WebRequest -Uri $Url -OutFile $DestPath -UseBasicParsing
        Write-Host " -> OK" -ForegroundColor Green
    } catch {
        Write-Host " -> Lỗi tải file $FileName" -ForegroundColor Red
        Write-Host $_.Exception.Message
    }
}

Write-Host "Đã hoàn thành! Tesseract đã có thể sử dụng các file best trained data mới."
