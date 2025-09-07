using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static DSO.Core.RegexLite.RegexLite;

namespace DSO.Core.RegexLite
{
    public sealed partial class RegexLite
    {
        // --- StringBuilder sonucu ---
        public readonly struct SbMatch
        {
            public readonly StringBuilder Source;
            public readonly int Index;
            public readonly int Length;

            public SbMatch(StringBuilder source, int index, int length)
            {
                Source = source;
                Index = index;
                Length = length;
            }

            public bool Success => Length >= 0;
            public int End => Index + Length;

            // İsteyince sadece bu parça string’e çevrilir
            public string Value => Length <= 0 ? string.Empty : Source.ToString(Index, Length);

            // Kopyasız okuma: hedef buffera yazar
            public void CopyTo(Span<char> destination)
            {
                if (destination.Length < Length) throw new ArgumentException("destination too small");
                Source.CopyTo(Index, destination, Length);
            }

            public override string ToString() => Value;

            public static SbMatch Empty(StringBuilder src) => new SbMatch(src, -1, -1);
        }

        // -------- PUBLIC API (StringBuilder) --------

        public SbMatch Match(StringBuilder sb, int startAt = 0)
        {
            if (sb is null) throw new ArgumentNullException(nameof(sb));

            // Ordinal dışı modlarda doğruluk için flatten (ToString) — istersen alternatif bir case-folding yolu eklenebilir
            if (comparison != StringComparison.Ordinal)
            {
                var m = Match(sb.ToString(), startAt);
                return m.Success ? new SbMatch(sb, m.Index, m.Length) : SbMatch.Empty(sb);
            }

            var src = new SbSource(sb);
            return SbScannerTryNext(src, this, Math.Max(0, startAt), out var m2) ? m2 : SbMatch.Empty(sb);
        }

        public List<SbMatch> Matches(StringBuilder sb, int startAt = 0, int maxMatches = int.MaxValue)
        {
            if (sb is null) throw new ArgumentNullException(nameof(sb));

            if (comparison != StringComparison.Ordinal)
            {
                // doğruluk için flatten
                var list = new List<SbMatch>();
                foreach (var m in Matches(sb.ToString(), startAt, maxMatches))
                    list.Add(new SbMatch(sb, m.Index, m.Length));
                return list;
            }

            var res = new List<SbMatch>();
            var src = new SbSource(sb);
            int pos = Math.Max(0, startAt);
            int remain = Math.Max(0, maxMatches);

            while (remain > 0 && SbScannerTryNext(src, this, pos, out var m))
            {
                res.Add(m);
                pos = m.End;
                remain--;
            }
            return res;
        }

        public bool IsMatch(StringBuilder sb, int startAt = 0) => Match(sb, startAt).Success;

        // Replace → yeni StringBuilder üretir (flatten etmeden)
        public StringBuilder Replace(StringBuilder sb, string replacement, int startAt = 0, int maxReplacements = int.MaxValue)
        {
            if (sb is null) throw new ArgumentNullException(nameof(sb));
            if (replacement is null) replacement = string.Empty;

            if (comparison != StringComparison.Ordinal)
            {
                var s = Replace(sb.ToString(), replacement, startAt, maxReplacements);
                return new StringBuilder(s);
            }

            var src = new SbSource(sb);
            var outSb = new StringBuilder(sb.Length);
            int prev = 0;
            int pos = Math.Max(0, startAt);
            int remain = Math.Max(0, maxReplacements);

            if (!SbScannerTryNext(src, this, pos, out var first))
            {
                AppendRange(outSb, sb, 0, sb.Length);
                return outSb;
            }

            AppendRange(outSb, sb, 0, first.Index);
            outSb.Append(replacement);
            prev = first.End;
            pos = prev;
            remain--;

            while (remain > 0 && SbScannerTryNext(src, this, pos, out var m))
            {
                AppendRange(outSb, sb, prev, m.Index - prev);
                outSb.Append(replacement);
                prev = m.End;
                pos = prev;
                remain--;
            }

            AppendRange(outSb, sb, prev, sb.Length - prev);
            return outSb;
        }

        public StringBuilder Replace(StringBuilder sb, Func<SbMatch, string> evaluator, int startAt = 0, int maxReplacements = int.MaxValue)
        {
            if (sb is null) throw new ArgumentNullException(nameof(sb));
            if (evaluator is null) throw new ArgumentNullException(nameof(evaluator));

            if (comparison != StringComparison.Ordinal)
            {
                var s = Replace(sb.ToString(), m => evaluator(new SbMatch(sb, m.Index, m.Length)), startAt, maxReplacements);
                return new StringBuilder(s);
            }

            var src = new SbSource(sb);
            var outSb = new StringBuilder(sb.Length);
            int prev = 0;
            int pos = Math.Max(0, startAt);
            int remain = Math.Max(0, maxReplacements);

            if (!SbScannerTryNext(src, this, pos, out var first))
            {
                AppendRange(outSb, sb, 0, sb.Length);
                return outSb;
            }

            AppendRange(outSb, sb, 0, first.Index);
            outSb.Append(evaluator(first) ?? string.Empty);
            prev = first.End;
            pos = prev;
            remain--;

            while (remain > 0 && SbScannerTryNext(src, this, pos, out var m))
            {
                AppendRange(outSb, sb, prev, m.Index - prev);
                outSb.Append(evaluator(m) ?? string.Empty);
                prev = m.End;
                pos = prev;
                remain--;
            }

            AppendRange(outSb, sb, prev, sb.Length - prev);
            return outSb;
        }

        // ================= Static convenience =================
        public static SbMatch Match(
            StringBuilder input, string open, string close,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0)
            => new RegexLite(open, close, requiredContains, excludedContains, includeBounds, longest, allowNesting, escapeChar, comparison)
                .Match(input, startAt);

        public static SbMatch Match(
            StringBuilder input, char open, char close,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0)
            => new RegexLite(open, close, requiredContains, excludedContains, includeBounds, longest, allowNesting, escapeChar, comparison)
                .Match(input, startAt);

        public static List<SbMatch> Matches(
            StringBuilder input, string open, string close,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0, int maxMatches = int.MaxValue)
            => new RegexLite(open, close, requiredContains, excludedContains, includeBounds, longest, allowNesting, escapeChar, comparison)
                .Matches(input, startAt, maxMatches);

        public static List<SbMatch> Matches(
            StringBuilder input, char open, char close,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0, int maxMatches = int.MaxValue)
            => new RegexLite(open, close, requiredContains, excludedContains, includeBounds, longest, allowNesting, escapeChar, comparison)
                .Matches(input, startAt, maxMatches);

        public static bool IsMatch(
            StringBuilder input, string open, string close,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0)
            => new RegexLite(open, close, requiredContains, excludedContains, includeBounds, longest, allowNesting, escapeChar, comparison)
                .IsMatch(input, startAt);

        public static bool IsMatch(
            StringBuilder input, char open, char close,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0)
            => new RegexLite(open, close, requiredContains, excludedContains, includeBounds, longest, allowNesting, escapeChar, comparison)
                .IsMatch(input, startAt);


        public static StringBuilder Replace(
            StringBuilder input, string open, string close, string replacement,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0, int maxReplacements = int.MaxValue)
            => new RegexLite(open, close, requiredContains, excludedContains, includeBounds, longest, allowNesting, escapeChar, comparison)
                .Replace(input, replacement, startAt, maxReplacements);

        public static StringBuilder Replace(
            StringBuilder input, char open, char close, string replacement,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0, int maxReplacements = int.MaxValue)
            => new RegexLite(open, close, requiredContains, excludedContains, includeBounds, longest, allowNesting, escapeChar, comparison)
                .Replace(input, replacement, startAt, maxReplacements);

        public static StringBuilder Replace(
            StringBuilder input, string open, string close, Func<SbMatch, string> evaluator,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0, int maxReplacements = int.MaxValue)
            => new RegexLite(open, close, requiredContains, excludedContains, includeBounds, longest, allowNesting, escapeChar, comparison)
                .Replace(input, evaluator, startAt, maxReplacements);

        public static StringBuilder Replace(
            StringBuilder input, char open, char close, Func<SbMatch, string> evaluator,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0, int maxReplacements = int.MaxValue)
            => new RegexLite(open, close, requiredContains, excludedContains, includeBounds, longest, allowNesting, escapeChar, comparison)
                .Replace(input, evaluator, startAt, maxReplacements);

        // ------------- İç kaynak (StringBuilder chunk’ları) -------------
        private sealed class SbSource
        {
            public readonly StringBuilder SB;
            public readonly List<ReadOnlyMemory<char>> Chunks = new();
            public readonly List<int> Starts = new();
            public readonly int Length;

            public SbSource(StringBuilder sb)
            {
                SB = sb ?? throw new ArgumentNullException(nameof(sb));
                int total = 0;
                foreach (ReadOnlyMemory<char> mem in sb.GetChunks())
                {
                    if (mem.Length == 0) continue;
                    Chunks.Add(mem);
                    Starts.Add(total);
                    total += mem.Length;
                }
                Length = total;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public char CharAt(int index)
            {
                Locate(index, out int ci, out int offset);
                return Chunks[ci].Span[offset];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Locate(int index, out int chunkIndex, out int offset)
            {
                int i = 0;
                for (; i < Starts.Count - 1; i++)
                {
                    if (index < Starts[i + 1]) break;
                }
                chunkIndex = i;
                offset = index - Starts[i];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadOnlySpan<char> ChunkSpan(int chunkIndex) => Chunks[chunkIndex].Span;

            public int IndexOfChar(char c, int start)
            {
                if (start >= Length) return -1;
                Locate(start, out int ci, out int off);
                for (int i = ci; i < Chunks.Count; i++)
                {
                    var span = ChunkSpan(i).Slice(i == ci ? off : 0);
                    int rel = span.IndexOf(c);
                    if (rel >= 0) return (i == ci ? start : Starts[i]) + rel;
                }
                return -1;
            }
        }

        // ------------- SB Scanner (Ordinal) -------------
        private static bool SbScannerTryNext(SbSource src, RegexLite rx, int startPos, out SbMatch match)
        {
            match = SbMatch.Empty(src.SB);
            int pos = startPos;
            if (pos >= src.Length) return false;

            if (rx.useCharTokens)
            {
                char oc = rx.openChar!.Value, cc = rx.closeChar!.Value;

                if (oc == cc) // toggle
                {
                    while (true)
                    {
                        int s = SbFindNextToken_Char(src, oc, pos, rx.escapeChar);
                        if (s < 0) return false;

                        int from = s + 1;
                        int e = SbFindNextToken_Char(src, cc, from, rx.escapeChar);
                        if (e < 0) return false;

                        if (SbPassesOrdinal(src, from, e - from, rx.requiredContains, rx.excludedContains))
                        {
                            int outStart = rx.includeBounds ? s : from;
                            int outLen = rx.includeBounds ? (e - s + 1) : (e - from);
                            match = new SbMatch(src.SB, outStart, outLen);
                            return true;
                        }
                        pos = e + 1;
                    }
                }
                else
                {
                    while (true)
                    {
                        int s = SbFindNextToken_Char(src, oc, pos, rx.escapeChar);
                        if (s < 0) return false;

                        int from = s + 1;
                        int e;

                        if (rx.allowNesting)
                        {
                            e = SbFindBalancedEnd_Char(src, from, oc, cc, rx.escapeChar);
                            if (e < 0) return false;
                        }
                        else if (!rx.longest)
                        {
                            e = SbFindNextToken_Char(src, cc, from, rx.escapeChar);
                            if (e < 0) return false;
                        }
                        else
                        {
                            int nextStart = SbFindNextToken_Char(src, oc, from, rx.escapeChar);
                            int searchEnd = nextStart < 0 ? src.Length : nextStart;
                            e = SbFindLastTokenBefore_Char(src, cc, from, searchEnd, rx.escapeChar);
                            if (e < 0) return false;
                        }

                        if (SbPassesOrdinal(src, from, e - from, rx.requiredContains, rx.excludedContains))
                        {
                            int outStart = rx.includeBounds ? s : from;
                            int outLen = rx.includeBounds ? (e - s + 1) : (e - from);
                            match = new SbMatch(src.SB, outStart, outLen);
                            return true;
                        }
                        pos = e + 1;
                    }
                }
            }
            else
            {
                ReadOnlySpan<char> sdl = rx.open.AsSpan();
                ReadOnlySpan<char> edl = rx.close.AsSpan();

                if (sdl.SequenceEqual(edl)) // toggle
                {
                    while (true)
                    {
                        int s = SbFindNextToken_String(src, sdl, pos, rx.escapeChar);
                        if (s < 0) return false;

                        int from = s + sdl.Length;
                        int e = SbFindNextToken_String(src, edl, from, rx.escapeChar);
                        if (e < 0) return false;

                        if (SbPassesOrdinal(src, from, e - from, rx.requiredContains, rx.excludedContains))
                        {
                            int outStart = rx.includeBounds ? s : from;
                            int outLen = rx.includeBounds ? (e + edl.Length - s) : (e - from);
                            match = new SbMatch(src.SB, outStart, outLen);
                            return true;
                        }
                        pos = e + edl.Length;
                    }
                }
                else
                {
                    while (true)
                    {
                        int s = SbFindNextToken_String(src, sdl, pos, rx.escapeChar);
                        if (s < 0) return false;

                        int from = s + sdl.Length;
                        int e;

                        if (rx.allowNesting)
                        {
                            e = SbFindBalancedEnd_String(src, from, sdl, edl, rx.escapeChar);
                            if (e < 0) return false;
                        }
                        else if (!rx.longest)
                        {
                            e = SbFindNextToken_String(src, edl, from, rx.escapeChar);
                            if (e < 0) return false;
                        }
                        else
                        {
                            int nextStart = SbFindNextToken_String(src, sdl, from, rx.escapeChar);
                            int searchEnd = nextStart < 0 ? src.Length : nextStart;
                            e = SbFindLastTokenBefore_String(src, edl, from, searchEnd, rx.escapeChar);
                            if (e < 0) return false;
                        }

                        if (SbPassesOrdinal(src, from, e - from, rx.requiredContains, rx.excludedContains))
                        {
                            int outStart = rx.includeBounds ? s : from;
                            int outLen = rx.includeBounds ? (e + edl.Length - s) : (e - from);
                            match = new SbMatch(src.SB, outStart, outLen);
                            return true;
                        }
                        pos = e + edl.Length;
                    }
                }
            }
        }

        // --------- SB token bulma (Ordinal) ---------
        private static int SbFindNextToken_Char(SbSource src, char tok, int start, char? esc)
        {
            int pos = start;
            while (true)
            {
                int i = src.IndexOfChar(tok, pos);
                if (i < 0) return -1;
                if (!SbIsEscaped(src, i, esc)) return i;
                pos = i + 1;
            }
        }

        private static int SbFindNextToken_String(SbSource src, ReadOnlySpan<char> tok, int start, char? esc)
        {
            int pos = start;
            char first = tok[0];
            while (true)
            {
                int i = src.IndexOfChar(first, pos);
                if (i < 0) return -1;
                if (!SbIsEscaped(src, i, esc) && SbMatchAt(src, i, tok))
                    return i;
                pos = i + 1;
            }
        }

        private static int SbFindLastTokenBefore_Char(SbSource src, char tok, int from, int searchEndOrMinus1, char? esc)
        {
            int end = (searchEndOrMinus1 < 0) ? src.Length : searchEndOrMinus1;
            int last = -1;
            int pos = from;
            while (true)
            {
                int i = src.IndexOfChar(tok, pos);
                if (i < 0 || i >= end) break;
                if (!SbIsEscaped(src, i, esc)) last = i;
                pos = i + 1;
            }
            return last;
        }

        private static int SbFindLastTokenBefore_String(SbSource src, ReadOnlySpan<char> tok, int from, int endExclusive, char? esc)
        {
            int last = -1;
            int pos = from;
            while (true)
            {
                int i = SbFindNextToken_String(src, tok, pos, esc);
                if (i < 0 || i >= endExclusive) break;
                last = i;
                pos = i + 1;
            }
            return last;
        }

        private static int SbFindBalancedEnd_Char(SbSource src, int from, char oc, char cc, char? esc)
        {
            int depth = 1;
            int pos = from;
            while (true)
            {
                int nOpen = SbFindNextToken_Char(src, oc, pos, esc);
                int nClose = SbFindNextToken_Char(src, cc, pos, esc);
                if (nClose < 0) return -1;
                if (nOpen >= 0 && nOpen < nClose) { depth++; pos = nOpen + 1; }
                else { depth--; if (depth == 0) return nClose; pos = nClose + 1; }
            }
        }

        private static int SbFindBalancedEnd_String(SbSource src, int from, ReadOnlySpan<char> sdl, ReadOnlySpan<char> edl, char? esc)
        {
            int depth = 1;
            int pos = from;
            while (true)
            {
                int nOpen = SbFindNextToken_String(src, sdl, pos, esc);
                int nClose = SbFindNextToken_String(src, edl, pos, esc);
                if (nClose < 0) return -1;
                if (nOpen >= 0 && nOpen < nClose) { depth++; pos = nOpen + sdl.Length; }
                else { depth--; if (depth == 0) return nClose; pos = nClose + edl.Length; }
            }
        }

        // --------- SB içerik filtresi (Ordinal) ---------
        private static bool SbPassesOrdinal(SbSource src, int start, int length, string must, IReadOnlyList<string> mustNot)
        {
            if (!string.IsNullOrEmpty(must) && SbIndexOfInRange(src, must.AsSpan(), start, start + length) < 0)
                return false;

            if (mustNot != null)
            {
                for (int i = 0; i < mustNot.Count; i++)
                {
                    var bad = mustNot[i];
                    if (!string.IsNullOrEmpty(bad) && SbIndexOfInRange(src, bad.AsSpan(), start, start + length) >= 0)
                        return false;
                }
            }
            return true;
        }

        private static int SbIndexOfInRange(SbSource src, ReadOnlySpan<char> needle, int from, int toExclusive)
        {
            if (needle.Length == 0) return from;
            char first = needle[0];
            int pos = from;
            while (pos < toExclusive)
            {
                int i = src.IndexOfChar(first, pos);
                if (i < 0 || i >= toExclusive) return -1;
                if (SbMatchAt(src, i, needle) && i + needle.Length <= toExclusive)
                    return i;
                pos = i + 1;
            }
            return -1;
        }

        // --------- SB yardımcılar ---------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SbMatchAt(SbSource src, int start, ReadOnlySpan<char> tok)
        {
            for (int k = 0; k < tok.Length; k++)
            {
                if (start + k >= src.Length) return false;
                if (src.CharAt(start + k) != tok[k]) return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SbIsEscaped(SbSource src, int tokenIndex, char? esc)
        {
            if (esc is null) return false;
            char e = esc.Value;
            int b = tokenIndex - 1;
            int streak = 0;
            while (b >= 0 && src.CharAt(b) == e) { streak++; b--; }
            return (streak & 1) == 1;
        }

        private static void AppendRange(StringBuilder dest, StringBuilder src, int start, int count)
        {
            if (count <= 0) return;
            const int Buf = 4096;
            char[] buffer = ArrayPool<char>.Shared.Rent(Math.Min(Buf, count));
            try
            {
                int remaining = count;
                int pos = start;
                while (remaining > 0)
                {
                    int chunk = Math.Min(buffer.Length, remaining);
                    src.CopyTo(pos, buffer, 0, chunk);
                    dest.Append(buffer, 0, chunk);
                    pos += chunk;
                    remaining -= chunk;
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }
    }

    public static partial class RegexLiteHelpers
    {
        public static SbMatch Match(
           this StringBuilder input, string open, string close,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0)
        {
            return RegexLite.Match(input, open, close, requiredContains, excludedContains, includeBounds
                , longest, allowNesting, escapeChar, comparison, startAt);
        }

        public static SbMatch Match(
           this StringBuilder input, char open, char close,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0)
        {
            return RegexLite.Match(input, open, close, requiredContains, excludedContains, includeBounds
                , longest, allowNesting, escapeChar, comparison, startAt);
        }

        public static List<SbMatch> Matches(
           this StringBuilder input, string open, string close,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0, int maxMatches = int.MaxValue)
        {
            return RegexLite.Matches(input, open, close, requiredContains, excludedContains
                , includeBounds, longest, allowNesting, escapeChar, comparison, startAt, maxMatches);
        }

        public static List<SbMatch> Matches(
           this StringBuilder input, char open, char close,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0, int maxMatches = int.MaxValue)
        {
            return RegexLite.Matches(input, open, close, requiredContains, excludedContains
                , includeBounds, longest, allowNesting, escapeChar, comparison, startAt, maxMatches);
        }

        public static bool IsMatch(
           this StringBuilder input, string open, string close,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0)
        {
            return RegexLite.IsMatch(input, open, close, requiredContains, excludedContains
                , includeBounds, longest
                , allowNesting, escapeChar, comparison, startAt);
        }

        public static bool IsMatch(
           this StringBuilder input, char open, char close,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0)
        {
            return RegexLite.IsMatch(input, open, close, requiredContains, excludedContains
                , includeBounds, longest
                , allowNesting, escapeChar, comparison, startAt);
        }

        public static StringBuilder Replace(
           this StringBuilder input, string open, string close, string replacement,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0, int maxReplacements = int.MaxValue)
        {
            return RegexLite.Replace(input, open, close, replacement, requiredContains
                , excludedContains, includeBounds, longest
                , allowNesting, escapeChar, comparison, startAt, maxReplacements);
        }

        public static StringBuilder Replace(
           this StringBuilder input, char open, char close, string replacement,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal,
            int startAt = 0, int maxReplacements = int.MaxValue)
        {
            return RegexLite.Replace(input, open, close, replacement, requiredContains
                , excludedContains, includeBounds, longest
                , allowNesting, escapeChar, comparison, startAt, maxReplacements);
        }

        public static StringBuilder Replace(
          this StringBuilder input, string open, string close, Func<SbMatch, string> evaluator,
           string requiredContains = null,
           IReadOnlyList<string> excludedContains = null,
           bool includeBounds = false,
           bool longest = false,
           bool allowNesting = false,
           char? escapeChar = null,
           StringComparison comparison = StringComparison.Ordinal,
           int startAt = 0, int maxReplacements = int.MaxValue)
        {
            return RegexLite.Replace(input, open, close, evaluator, requiredContains, excludedContains
                , includeBounds
                , longest, allowNesting, escapeChar, comparison, startAt, maxReplacements);
        }

        public static StringBuilder Replace(
          this StringBuilder input, char open, char close, Func<SbMatch, string> evaluator,
           string requiredContains = null,
           IReadOnlyList<string> excludedContains = null,
           bool includeBounds = false,
           bool longest = false,
           bool allowNesting = false,
           char? escapeChar = null,
           StringComparison comparison = StringComparison.Ordinal,
           int startAt = 0, int maxReplacements = int.MaxValue)
        {
            return RegexLite.Replace(input, open, close, evaluator, requiredContains, excludedContains
                , includeBounds
                , longest, allowNesting, escapeChar, comparison, startAt, maxReplacements);
        }
    }
}