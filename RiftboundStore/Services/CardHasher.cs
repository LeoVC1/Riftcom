using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RiftboundStore.Services;

public interface ICardHasher
{
    Task<string?> ComputeAsync(string imageUrl, CancellationToken ct = default);
}

/// <summary>
/// Computes a 64-bit dHash of a card image. Uses the same 9x8 grayscale + horizontal-diff
/// algorithm as the client-side JS in the scanner, so hashes are comparable across sides.
///
/// Output: 16-char lowercase hex string (64 bits).
/// </summary>
public class CardHasher : ICardHasher
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<CardHasher> _logger;

    public CardHasher(IHttpClientFactory http, ILogger<CardHasher> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string?> ComputeAsync(string imageUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;
        var client = _http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        byte[] bytes;
        try
        {
            bytes = await client.GetByteArrayAsync(imageUrl, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao baixar {Url}", imageUrl);
            return null;
        }

        try
        {
            using var img = Image.Load<Rgba32>(bytes);
            img.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(9, 8),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Bicubic
            }));

            // Compute 8 rows × 8 diffs (adjacent horizontal pixels), 1 bit each → 64 bits.
            ulong bits = 0UL;
            int bitIndex = 0;
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    var left = img[x, y];
                    var right = img[x + 1, y];
                    int grayL = (left.R * 299 + left.G * 587 + left.B * 114) / 1000;
                    int grayR = (right.R * 299 + right.G * 587 + right.B * 114) / 1000;
                    if (grayL > grayR)
                    {
                        bits |= (1UL << (63 - bitIndex));
                    }
                    bitIndex++;
                }
            }
            return bits.ToString("x16");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao processar imagem {Url}", imageUrl);
            return null;
        }
    }

    /// <summary>Hamming distance between two hex hashes (or int.MaxValue if invalid).</summary>
    public static int Distance(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a.Length != b.Length)
            return int.MaxValue;
        try
        {
            ulong av = Convert.ToUInt64(a, 16);
            ulong bv = Convert.ToUInt64(b, 16);
            return System.Numerics.BitOperations.PopCount(av ^ bv);
        }
        catch { return int.MaxValue; }
    }
}
