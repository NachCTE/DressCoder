using System.Runtime.InteropServices;

namespace DressCoder.Infrastructure.IoStore;

/// <summary>
/// P/Invoke wrapper around the Oodle compression library (oo2core_*.dll), ported from
/// the ctypes calls in FFVII-Rebirth-Mesh-Patcher's lib/iostore.py (MIT license, see
/// docs/04-licencias-terceros.md). NEVER bundles the DLL itself — it is proprietary
/// (RAD Game Tools/Epic Games); the caller must locate a copy from the user's own game
/// installation (see <see cref="ExternalTools.OodleLibraryResolver"/>).
///
/// We do not need to match the original encoder's exact codec/level: the game reads
/// whichever codec/level a compressed block declares in its own header (verified across
/// oo2core 6/7/9 by the reference project). We use Kraken at a middling level and always
/// verify the compressed bytes round-trip before trusting them.
/// </summary>
public sealed class OodleCompression : IDisposable
{
    private const int OodleLzKraken = 8;
    private const int OodleLevelNormal = 4;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long OodleLzDecompressDelegate(
        byte[] srcBuf, long srcSize,
        byte[] rawBuf, long rawSize,
        int fuzzSafe, int checkCrc, int verbosity,
        IntPtr decBufBase, long decBufSize, IntPtr fpCallback,
        IntPtr callbackUserData, IntPtr decoderMemory, long decoderMemorySize,
        int threadPhase);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long OodleLzCompressDelegate(
        int compressor, byte[] srcBuf, long srcSize, byte[] dstBuf,
        int level, IntPtr options, IntPtr dictionaryBase, IntPtr lrm,
        IntPtr scratchMemory, long scratchSize);

    private readonly IntPtr _moduleHandle;
    private readonly OodleLzDecompressDelegate _decompress;
    private readonly OodleLzCompressDelegate? _compress;

    private OodleCompression(IntPtr moduleHandle, OodleLzDecompressDelegate decompress, OodleLzCompressDelegate? compress)
    {
        _moduleHandle = moduleHandle;
        _decompress = decompress;
        _compress = compress;
    }

    /// <summary>Loads oo2core_*.dll from the given path. Throws with an actionable message on failure.</summary>
    public static OodleCompression Load(string dllPath)
    {
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException($"No se encontró la librería Oodle en '{dllPath}'.");
        }

        var handle = NativeLibrary.Load(dllPath);
        try
        {
            var decompressPtr = NativeLibrary.GetExport(handle, "OodleLZ_Decompress");
            var decompress = Marshal.GetDelegateForFunctionPointer<OodleLzDecompressDelegate>(decompressPtr);

            OodleLzCompressDelegate? compress = null;
            if (NativeLibrary.TryGetExport(handle, "OodleLZ_Compress", out var compressPtr))
            {
                compress = Marshal.GetDelegateForFunctionPointer<OodleLzCompressDelegate>(compressPtr);
            }

            return new OodleCompression(handle, decompress, compress);
        }
        catch
        {
            NativeLibrary.Free(handle);
            throw;
        }
    }

    /// <summary>Decompresses <paramref name="src"/> into exactly <paramref name="outSize"/> bytes.</summary>
    public byte[] Decompress(byte[] src, int outSize)
    {
        var outBuf = new byte[outSize];
        // Trailing args are fuzz-safety/threading options unused here — the same values
        // Unreal itself passes (ported verbatim from oodle_decompress in iostore.py).
        var n = _decompress(src, src.LongLength, outBuf, outSize,
            1, 1, 0, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, 3);

        if (n <= 0)
        {
            throw new InvalidOperationException(
                $"Oodle no pudo decodificar estos datos (devolvió {n}). La DLL oo2core probablemente " +
                "sea demasiado vieja o incompatible — usar oo2core_6 o más nueva.");
        }

        if (n != outSize)
        {
            throw new InvalidOperationException(
                $"Oodle devolvió {n} bytes pero se esperaban {outSize}. Esto casi siempre significa que " +
                "la tabla de bloques se interpretó incorrectamente.");
        }

        return outBuf;
    }

    /// <summary>
    /// Kraken-compresses <paramref name="src"/>, or null if the DLL exposes no compressor
    /// or the call failed. The caller must decide whether the result actually helped and
    /// verify it round-trips (see <see cref="TryCompressVerified"/>).
    /// </summary>
    public byte[]? Compress(byte[] src)
    {
        if (_compress is null) return null;

        var bound = src.Length + src.Length / 16 + 512;
        var outBuf = new byte[bound];
        var n = _compress(OodleLzKraken, src, src.LongLength, outBuf, OodleLevelNormal,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0);

        return n > 0 ? outBuf[..(int)n] : null;
    }

    /// <summary>
    /// Compresses <paramref name="src"/> and verifies the result decompresses back to the
    /// exact original before returning it. Returns null if compression isn't available,
    /// doesn't shrink the data, or fails to round-trip — callers should fall back to
    /// storing the block uncompressed (method 0) in that case, mirroring
    /// <c>_pack_blocks</c> in writer.py/patch.py.
    /// </summary>
    public byte[]? TryCompressVerified(byte[] src)
    {
        var compressed = Compress(src);
        if (compressed is null || compressed.Length >= src.Length) return null;

        try
        {
            var roundTrip = Decompress(compressed, src.Length);
            return roundTrip.AsSpan().SequenceEqual(src) ? compressed : null;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_moduleHandle != IntPtr.Zero)
        {
            NativeLibrary.Free(_moduleHandle);
        }
    }
}
