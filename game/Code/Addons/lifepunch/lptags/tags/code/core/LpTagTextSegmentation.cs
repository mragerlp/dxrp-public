// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
// PROPRIETARY & CONFIDENTIAL â€” Â© 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCHâ„¢ Tags for DXRP" (s&box ident: lifepunch.tags Â· addon ident: lptags) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity â€” including DXRP and
// LifePunch staff, contributors, or community â€” EXCEPT the owner (lifepunch.co).
// Third-party material identified by an accompanying notice remains under its stated license.
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
//
// Author account: mrragerlp Â· Public alias (in-game Â· Steam Â· Discord): Bloodwave
// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LifePunch.DXRP.Addons.Tags;

public readonly record struct LpTagTextElement( int Start, int Length );

public sealed class LpTagTextAnalysis
{
    private readonly LpTagTextElement[] _elements;

    public string OriginalText { get; }
    public int GraphemeCount { get; }
    public bool RequiresWholeNameFallback { get; }
    public ReadOnlySpan<LpTagTextElement> Elements => _elements;

    internal LpTagTextAnalysis(
        string originalText,
        int graphemeCount,
        bool requiresWholeNameFallback,
        LpTagTextElement[] elements )
    {
        OriginalText = originalText;
        GraphemeCount = graphemeCount;
        RequiresWholeNameFallback = requiresWholeNameFallback;
        _elements = elements;
    }
}

public static class LpTagTextSegmentation
{
    private const int MaximumGraphemeCount = 64;
    private const int MaximumUtf8ByteCount = 256;

    private static readonly LpTagTextAnalysis Empty = new(
        string.Empty,
        0,
        false,
        Array.Empty<LpTagTextElement>() );

    public static LpTagTextAnalysis Analyze( string displayName )
    {
        if ( string.IsNullOrEmpty( displayName ) )
            return Empty;

        var elements = new List<LpTagTextElement>( Math.Min( displayName.Length, MaximumGraphemeCount ) );
        var graphemeCount = 0;
        var segmentationUnsafe = false;

        try
        {
            var enumerator = StringInfo.GetTextElementEnumerator( displayName );
            var previousStart = -1;
            while ( enumerator.MoveNext() )
            {
                var currentStart = enumerator.ElementIndex;
                if ( currentStart < 0
                    || currentStart >= displayName.Length
                    || currentStart <= previousStart
                    || ( previousStart < 0 && currentStart != 0 ) )
                {
                    segmentationUnsafe = true;
                    break;
                }

                if ( previousStart >= 0 )
                    AddElement( elements, previousStart, currentStart - previousStart, ref graphemeCount );

                previousStart = currentStart;
            }

            if ( !segmentationUnsafe && previousStart >= 0 )
                AddElement( elements, previousStart, displayName.Length - previousStart, ref graphemeCount );

            if ( previousStart < 0 )
                segmentationUnsafe = true;
        }
        catch ( ArgumentException )
        {
            segmentationUnsafe = true;
        }

        var scalarUnsafe = ContainsUnsafeScalar( displayName );
        var exceedsUtf8Limit = false;
        try
        {
            exceedsUtf8Limit = Encoding.UTF8.GetByteCount( displayName ) > MaximumUtf8ByteCount;
        }
        catch ( ArgumentException )
        {
            segmentationUnsafe = true;
        }

        var requiresFallback = segmentationUnsafe
            || scalarUnsafe
            || graphemeCount > MaximumGraphemeCount
            || exceedsUtf8Limit;

        return new LpTagTextAnalysis(
            displayName,
            graphemeCount,
            requiresFallback,
            elements.Count == 0 ? Array.Empty<LpTagTextElement>() : elements.ToArray() );
    }

    private static void AddElement(
        List<LpTagTextElement> elements,
        int start,
        int length,
        ref int graphemeCount )
    {
        graphemeCount++;
        if ( graphemeCount <= MaximumGraphemeCount )
            elements.Add( new LpTagTextElement( start, length ) );
    }

    private static bool ContainsUnsafeScalar( string text )
    {
        for ( var index = 0; index < text.Length; index++ )
        {
            var first = text[index];
            int scalar;
            if ( char.IsHighSurrogate( first ) )
            {
                if ( index + 1 >= text.Length || !char.IsLowSurrogate( text[index + 1] ) )
                    return true;

                scalar = char.ConvertToUtf32( first, text[++index] );
            }
            else
            {
                if ( char.IsLowSurrogate( first ) )
                    return true;

                scalar = first;
            }

            if ( IsBidiControl( scalar ) || IsConservativeContextualRange( scalar ) )
                return true;
        }

        return false;
    }

    private static bool IsBidiControl( int scalar )
    {
        return scalar == 0x061c
            || scalar is >= 0x200e and <= 0x200f
            || scalar is >= 0x202a and <= 0x202e
            || scalar is >= 0x2066 and <= 0x2069;
    }

    private static bool IsConservativeContextualRange( int scalar )
    {
        return scalar is >= 0x0590 and <= 0x08ff
            || scalar is >= 0x0900 and <= 0x0dff
            || scalar is >= 0xfb1d and <= 0xfdff
            || scalar is >= 0xfe70 and <= 0xfeff;
    }
}
