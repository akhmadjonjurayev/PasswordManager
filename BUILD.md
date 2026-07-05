# SecureVault - Qurilish va ishga tushirish

## Talablar

- .NET 10 SDK (https://dotnet.microsoft.com/download/dotnet/10.0)
- MAUI workload: `dotnet workload install maui-windows`
- Visual Studio 2022 17.12+ yoki Rider (ixtiyoriy)

## Ishga tushirish

# poweshell da publish.ps1 ni ishga tushirishdan oldin yozish kerak
Set-ExecutionPolicy Unrestricted -Scope Process

```bash
# Loyiha papkasiga o'ting
cd D:\Claude\PasswordManager

# MAUI workload o'rnating (bir marta)
dotnet workload install maui-windows

# Paketlarni tiklash
dotnet restore

# Ishga tushirish (unpackaged Windows app)
dotnet run --framework net10.0-windows10.0.19041.0
```

## Ma'lumotlar qayerda saqlanadi?

```
%APPDATA%\SecureVault\vault.db
```

## Xavfsizlik

- **Shifrlash**: AES-256-CBC (har bir yozuvga noyob IV)
- **Kalit hosil qilish**: PBKDF2-HMAC-SHA512, 310,000 iteratsiya
- **Parol tekshirish**: PBKDF2-HMAC-SHA512 (shifrlash kalitidan alohida)
- **Avtomatik qulflash**: 10 daqiqa harakatsizlik

## Eslatma OleDB haqida

Siz OleDB so'radingiz, lekin SQLite ishlatildi, sababi:
- SQLite hamma Windows-da ishlaydi (qo'shimcha driver kerak emas)
- OleDB (ACE OLEDB 12.0) uchun Microsoft Access Database Engine alohida o'rnatilishi kerak
- SQLite bitta fayl, portativ, tez
