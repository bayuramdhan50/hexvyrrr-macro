# PowerShell script untuk digital signing binary dengan publisher 'hexvyrr'
param (
    [string]$TargetExe = "F:\Bayu\Koding\1 hari 1 aplikasi\pb-recoil\bin\Release\net8.0-windows\HexvyrrMacro.exe"
)

$certSubject = "CN=hexvyrr, O=hexvyrr, OU=Hexvyrr Macro Engine"

Write-Host "[*] Memeriksa Code Signing Certificate untuk publisher: hexvyrr..." -ForegroundColor Cyan

$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Subject -like "*hexvyrr*" } | Select-Object -First 1

if (-not $cert) {
    Write-Host "[+] Membuat Code Signing Certificate baru atas nama 'hexvyrr'..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate -Type CodeSigningCert `
        -Subject $certSubject `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -NotAfter (Get-Date).AddYears(15) `
        -KeyExportPolicy Exportable `
        -FriendlyName "hexvyrr Code Signing Certificate"

    try {
        $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store([System.Security.Cryptography.X509Certificates.StoreName]::Root, [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
        $rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $rootStore.Add($cert)
        $rootStore.Close()

        $pubStore = New-Object System.Security.Cryptography.X509Certificates.X509Store([System.Security.Cryptography.X509Certificates.StoreName]::TrustedPublisher, [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
        $pubStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $pubStore.Add($cert)
        $pubStore.Close()

        Write-Host "[+] Sertifikat 'hexvyrr' berhasil didaftarkan ke Trusted Root dan Trusted Publishers." -ForegroundColor Green
    } catch {
        Write-Warning "Gagal import otomatis ke Trusted store: $_"
    }
} else {
    Write-Host "[+] Sertifikat 'hexvyrr' aktif: $($cert.Thumbprint)" -ForegroundColor Green
}

# Jika target adalah DLL, cari juga file EXE pasangannya
$exePath = [System.IO.Path]::ChangeExtension($TargetExe, ".exe")
$filesToSign = @()

if (Test-Path $TargetExe) { $filesToSign += $TargetExe }
if ((Test-Path $exePath) -and ($exePath -ne $TargetExe)) { $filesToSign += $exePath }

foreach ($file in $filesToSign) {
    Write-Host "[*] Menandatangani digital: $file" -ForegroundColor Cyan
    $sig = Set-AuthenticodeSignature -Certificate $cert -FilePath $file -HashAlgorithm SHA256
    Write-Host "[OK] Status signature ($file): $($sig.Status)" -ForegroundColor Green
}
