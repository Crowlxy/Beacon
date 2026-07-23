// Adapted from Beacon-old/Flow.Launcher.Infrastructure/DiacriticsNormalizer.cs.
// Copyright (c) Flow Launcher and Wox contributors. Licensed under the MIT License.
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Beacon.Core;

public static class DiacriticsNormalizer
{
    private static readonly Dictionary<char, char> AccentMap = new()
    {
        ['á'] = 'a', ['à'] = 'a', ['ã'] = 'a', ['â'] = 'a', ['ä'] = 'a', ['å'] = 'a',
        ['é'] = 'e', ['è'] = 'e', ['ê'] = 'e', ['ë'] = 'e',
        ['í'] = 'i', ['ì'] = 'i', ['î'] = 'i', ['ï'] = 'i',
        ['ó'] = 'o', ['ò'] = 'o', ['õ'] = 'o', ['ô'] = 'o', ['ö'] = 'o', ['ø'] = 'o',
        ['ú'] = 'u', ['ù'] = 'u', ['û'] = 'u', ['ü'] = 'u',
        ['ç'] = 'c', ['ñ'] = 'n', ['ý'] = 'y', ['ÿ'] = 'y', ['ß'] = 's',
        ['ł'] = 'l', ['æ'] = 'a', ['œ'] = 'o',
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static char NormalizeChar(char value)
    {
        value = char.ToLowerInvariant(value);
        return AccentMap.GetValueOrDefault(value, value);
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        char[]? rented = null;
        Span<char> buffer = value.Length <= 512 ? stackalloc char[value.Length] : (rented = ArrayPool<char>.Shared.Rent(value.Length));
        try
        {
            for (var index = 0; index < value.Length; index++) buffer[index] = NormalizeChar(value[index]);
            return new string(buffer[..value.Length]);
        }
        finally
        {
            if (rented is not null) ArrayPool<char>.Shared.Return(rented);
        }
    }
}
