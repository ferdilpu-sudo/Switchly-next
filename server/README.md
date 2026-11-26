# Switchly License API (Oracle Cloud Always Free Ampere A1 + Midtrans Snap)

Backend ringan berbasis **ASP.NET Core 10 Minimal API + PostgreSQL 16 + Caddy (auto-HTTPS)** untuk mengelola:
- Autentikasi OTP Email opsional (khusus Trial 7 Hari & Pro; mode **Free 5 profil** tetap 100% lokal tanpa login).
- Anti-abuse Trial 7 Hari (**1× per email** dan **1× per `device_hash`**).
- Checkout **Midtrans Snap** (QRIS, Virtual Account, GoPay/ShopeePay, Kartu Kredit) & verifikasi webhook `SHA-512` idempoten.
- Penerbitan **Ed25519 Signed License Token** (verifikasi offline di Switchly Desktop dengan *grace period* 14 hari).

## Deploy Cepat di Oracle Cloud Ampere A1 (Ubuntu 24.04 ARM64)

1. **Buka port di OCI Security List & `ufw`:**
   - Ingress TCP `80` dan `443` (untuk Caddy Let's Encrypt HTTPS).
   - Arahkan DNS `A` record `api.switchly.id` ke Public IP VM OCI.

2. **Install Docker Engine & Compose:**
   ```bash
   curl -fsSL https://get.docker.com | sh
   sudo usermod -aG docker $USER
   ```

3. **Generate Ed25519 Keypair & siapkan `.env`:**
   ```bash
   cd server
   cp .env.example .env
   docker compose build api
   docker compose run --rm api --generate-keys
   ```
   Salin `LICENSE_ED25519_PRIVATE_KEY` dan `LICENSE_TOKEN_HASH_PEPPER` ke `server/.env`, lalu simpan `SWITCHLY_ED25519_PUBLIC_KEY` untuk ditanam di Switchly Desktop.

4. **Jalankan stack:**
   ```bash
   chmod 600 .env
   docker compose up -d
   curl https://api.switchly.id/healthz
   ```

5. **Atur Webhook di Midtrans Dashboard (Settings → Configuration → Payment Notification URL):**
   ```text
   https://api.switchly.id/v1/webhooks/payment/midtrans
   ```

## Backup Harian PostgreSQL (Disarankan)
Tambahkan cron harian di VM OCI:
```bash
0 3 * * * docker exec server-postgres-1 pg_dump -U switchly switchly_license | gzip > /home/ubuntu/backups/switchly_$(date +\%F).sql.gz
```
