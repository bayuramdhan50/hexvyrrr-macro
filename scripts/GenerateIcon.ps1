Add-Type -AssemblyName System.Drawing

$sourcePath = 'C:\Users\Bayu\.gemini\antigravity-ide\brain\e6ab9052-802a-4d03-afa4-5e54fa466497\hexvyrr_logo_1786956199602.jpg'
$destDir = 'f:\Bayu\Koding\1 hari 1 aplikasi\pb-recoil\Resources'

if (-not (Test-Path $destDir)) {
    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
}

$destPng = Join-Path $destDir 'app_logo.png'
$destIco = Join-Path $destDir 'app_icon.ico'

# Simpan PNG kualitas tinggi
$srcBmp = [System.Drawing.Bitmap]::FromFile($sourcePath)
$srcBmp.Save($destPng, [System.Drawing.Imaging.ImageFormat]::Png)

# Buat file ICO multi-resolusi
$sizes = @(256, 128, 64, 48, 32, 16)
$icoStream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($icoStream)

# Header: Reserved (0), Type (1=Icon), Count
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$sizes.Count)

$imageStreams = @()
foreach ($sz in $sizes) {
    $resized = New-Object System.Drawing.Bitmap($srcBmp, (New-Object System.Drawing.Size($sz, $sz)))
    $pngStream = New-Object System.IO.MemoryStream
    $resized.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $imageStreams += $pngStream
    $resized.Dispose()
}

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $stream = $imageStreams[$i]
    $bWidth = if ($sz -ge 256) { 0 } else { [byte]$sz }
    $bHeight = if ($sz -ge 256) { 0 } else { [byte]$sz }

    $writer.Write([byte]$bWidth)
    $writer.Write([byte]$bHeight)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$stream.Length)
    $writer.Write([UInt32]$offset)
    $offset += $stream.Length
}

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $bytes = $imageStreams[$i].ToArray()
    $writer.Write($bytes)
    $imageStreams[$i].Dispose()
}

$srcBmp.Dispose()
[System.IO.File]::WriteAllBytes($destIco, $icoStream.ToArray())
$writer.Dispose()
$icoStream.Dispose()

Write-Host "[OK] Icon & Logo berhasil dibuat di $destDir"
