using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Switchly.Application.Security;

namespace Switchly.Infrastructure.Security;

/// <summary>
/// Windows DPAPI (CurrentUser) implementation via crypt32.dll without external package dependencies (TASK-029, ADR-005).
/// Fails closed with CryptographicException when decryption fails or is attempted by another Windows account.
/// </summary>
public sealed class WindowsDpapiProtector : ILocalDataProtector
{
    private const uint CryptProtectUiForbidden = 0x1;

    public byte[] Protect(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> entropy = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows DPAPI is only supported on Windows.");
        }

        var inBlob = AllocateBlob(plaintext);
        var entropyBlob = entropy.IsEmpty ? default : AllocateBlob(entropy);
        DATA_BLOB outBlob = default;

        try
        {
            var ok = entropy.IsEmpty
                ? CryptProtectData(ref inBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out outBlob)
                : CryptProtectDataWithEntropy(ref inBlob, null, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out outBlob);

            if (!ok)
            {
                throw new CryptographicException($"DPAPI CryptProtectData failed (Win32 error {Marshal.GetLastWin32Error()}).");
            }

            return CopyAndFreeOutputBlob(outBlob);
        }
        finally
        {
            FreeInputBlob(inBlob);
            FreeInputBlob(entropyBlob);
        }
    }

    public byte[] Unprotect(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> entropy = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows DPAPI is only supported on Windows.");
        }

        if (ciphertext.IsEmpty)
        {
            throw new CryptographicException("Ciphertext is empty.");
        }

        var inBlob = AllocateBlob(ciphertext);
        var entropyBlob = entropy.IsEmpty ? default : AllocateBlob(entropy);
        DATA_BLOB outBlob = default;

        try
        {
            var ok = entropy.IsEmpty
                ? CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out outBlob)
                : CryptUnprotectDataWithEntropy(ref inBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out outBlob);

            if (!ok)
            {
                throw new CryptographicException($"DPAPI CryptUnprotectData failed (Win32 error {Marshal.GetLastWin32Error()}).");
            }

            return CopyAndFreeOutputBlob(outBlob);
        }
        finally
        {
            FreeInputBlob(inBlob);
            FreeInputBlob(entropyBlob);
        }
    }

    private static DATA_BLOB AllocateBlob(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return default;
        }

        var ptr = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data.ToArray(), 0, ptr, data.Length);
        return new DATA_BLOB { cbData = (uint)data.Length, pbData = ptr };
    }

    private static void FreeInputBlob(DATA_BLOB blob)
    {
        if (blob.pbData != IntPtr.Zero)
        {
            if (blob.cbData > 0)
            {
                var zeros = new byte[blob.cbData];
                Marshal.Copy(zeros, 0, blob.pbData, zeros.Length);
            }

            Marshal.FreeHGlobal(blob.pbData);
        }
    }

    private static byte[] CopyAndFreeOutputBlob(DATA_BLOB blob)
    {
        try
        {
            if (blob.pbData == IntPtr.Zero || blob.cbData == 0)
            {
                return Array.Empty<byte>();
            }

            var result = new byte[blob.cbData];
            Marshal.Copy(blob.pbData, result, 0, result.Length);
            return result;
        }
        finally
        {
            if (blob.pbData != IntPtr.Zero)
            {
                if (blob.cbData > 0)
                {
                    var zeros = new byte[blob.cbData];
                    Marshal.Copy(zeros, 0, blob.pbData, zeros.Length);
                }

                LocalFree(blob.pbData);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public uint cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn,
        string? szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        uint dwFlags,
        out DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", EntryPoint = "CryptProtectData", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectDataWithEntropy(
        ref DATA_BLOB pDataIn,
        string? szDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        uint dwFlags,
        out DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        uint dwFlags,
        out DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", EntryPoint = "CryptUnprotectData", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectDataWithEntropy(
        ref DATA_BLOB pDataIn,
        IntPtr ppszDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        uint dwFlags,
        out DATA_BLOB pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
