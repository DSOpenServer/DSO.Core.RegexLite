using System.Runtime.CompilerServices;
using System.Text;
using static DSO.Core.RegexLite.RegexLite;

namespace DSO.Core.RegexLite
{
    public sealed partial class RegexLite
    {
        // ---------------- Public Match ----------------
        public readonly struct LiteMatch
        {
            public readonly int Index;
            public readonly int Length;
            public readonly ReadOnlyMemory<char> Memory;

            public LiteMatch(int index, int length, ReadOnlyMemory<char> memory)
            {
                Index = index;
                Length = length;
                Memory = memory;
            }

            public bool Success => Length >= 0;
            public int End => Index + Length;
            public ReadOnlySpan<char> Span => Memory.Span;
            public string Value => Memory.ToString();
            public static LiteMatch Empty => new LiteMatch(-1, -1, ReadOnlyMemory<char>.Empty);
            public override string ToString() => Success ? Value : string.Empty;
        }

        // ---------------- Options ----------------
        public string open { get; }
        public string close { get; }
        public bool useCharTokens { get; }
        public char? openChar { get; }
        public char? closeChar { get; }

        public string requiredContains { get; }
        public IReadOnlyList<string> excludedContains { get; }
        public bool includeBounds { get; }
        public bool longest { get; }       // greedy yerine
        public bool allowNesting { get; }  // dengeli eşleşme
        public char? escapeChar { get; }   // örn. '\'
        public StringComparison comparison { get; }

        // ------------- Ctors -------------
        public RegexLite(
            string open,
            string close,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(open)) throw new ArgumentException(nameof(open));
            if (string.IsNullOrEmpty(close)) throw new ArgumentException(nameof(close));

            this.open = open;
            this.close = close;
            this.useCharTokens = false;
            this.openChar = null;
            this.closeChar = null;

            this.requiredContains = requiredContains;
            this.excludedContains = excludedContains;
            this.includeBounds = includeBounds;
            this.longest = longest;
            this.allowNesting = allowNesting;
            this.escapeChar = escapeChar;
            this.comparison = comparison;
        }

        public RegexLite(
            char open,
            char close,
            string requiredContains = null,
            IReadOnlyList<string> excludedContains = null,
            bool includeBounds = false,
            bool longest = false,
            bool allowNesting = false,
            char? escapeChar = null,
            StringComparison comparison = StringComparison.Ordinal)
        {
            this.open = open.ToString();
            this.close = close.ToString();
            this.useCharTokens = true;
            this.openChar = open;
            this.closeChar = close;

            this.requiredContains = requiredContains;
            this.excludedContains = excludedContains;
            this.includeBounds = includeBounds;
            this.longest = longest;
            this.allowNesting = allowNesting;
            this.escapeChar = escapeChar;
            this.comparison = comparison;
        }

        // ================= Instance API =================
        public LiteMatch Match(string input, int startAt = 0)
        {
            var sc = new Scanner(this, input, startAt, 1);
            return sc.TryNext(out var m) ? m : LiteMatch.Empty;
        }

        public List<LiteMatch> Matches(string input, int startAt = 0, int maxMatches = int.MaxValue)
        {
            var list = new List<LiteMatch>();
            var sc = new Scanner(this, input, startAt, maxMatches);
            while (sc.TryNext(out var m)) list.Add(m);
            return list;
        }

        public bool IsMatch(string input, int startAt = 0)
        {
            var sc = new Scanner(this, input, startAt, 1);
            return sc.TryNext(out _);
        }

        // Replace — sabit metin
        public string Replace(string input, string replacement, int startAt = 0, int maxReplacements = int.MaxValue)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (replacement is null) replacement = string.Empty;

            var sc = new Scanner(this, input, startAt, maxReplacements);
            if (!sc.TryNext(out var first)) return input;

            var sb = new StringBuilder(input.Length);
            int prev = 0;

            // ilk match’i de yazalım
            sb.Append(input, prev, first.Index - prev);
            sb.Append(replacement);
            prev = first.End;

            while (sc.TryNext(out var m))
            {
                sb.Append(input, prev, m.Index - prev);
                sb.Append(replacement);
                prev = m.End;
            }
            sb.Append(input, prev, input.Length - prev);
            return sb.ToString();
        }

        // Replace — evaluator
        public string Replace(string input, Func<LiteMatch, string> evaluator, int startAt = 0, int maxReplacements = int.MaxValue)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (evaluator is null) throw new ArgumentNullException(nameof(evaluator));

            var sc = new Scanner(this, input, startAt, maxReplacements);
            if (!sc.TryNext(out var first)) return input;

            var sb = new StringBuilder(input.Length);
            int prev = 0;

            sb.Append(input, prev, first.Index - prev);
            sb.Append(evaluator(first) ?? string.Empty);
            prev = first.End;

            while (sc.TryNext(out var m))
            {
                sb.Append(input, prev, m.Index - prev);
                sb.Append(evaluator(m) ?? string.Empty);
                prev = m.End;
            }
            sb.Append(input, prev, input.Length - prev);
            return sb.ToString();
        }

        // ================= Static convenience =================
        public static LiteMatch Match(
            string input, string open, string close,
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

        public static LiteMatch Match(
            string input, char open, char close,
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

        public static List<LiteMatch> Matches(
            string input, string open, string close,
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

        public static List<LiteMatch> Matches(
            string input, char open, char close,
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
            string input, string open, string close,
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
            string input, char open, char close,
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


        public static string Replace(
            string input, string open, string close, string replacement,
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

        public static string Replace(
            string input, char open, char close, string replacement,
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

        public static string Replace(
            string input, string open, string close, Func<LiteMatch, string> evaluator,
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

        public static string Replace(
            string input, char open, char close, Func<LiteMatch, string> evaluator,
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

        // ================== Scanner (Span içeride, iterator DEĞİL) ==================
        private struct Scanner
        {
            private readonly RegexLite _rx;
            private readonly string _input;
            private int _pos;
            private int _remaining;

            public Scanner(RegexLite rx, string input, int startAt, int max)
            {
                _rx = rx;
                _input = input ?? string.Empty;
                _pos = Math.Max(0, Math.Min(startAt, _input.Length));
                _remaining = Math.Max(0, max);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryNext(out LiteMatch match)
            {
                match = LiteMatch.Empty;
                if (_remaining <= 0 || _pos >= _input.Length) return false;

                bool ordinal = _rx.comparison == StringComparison.Ordinal;

                if (_rx.useCharTokens)
                {
                    var oc = _rx.openChar!.Value;
                    var cc = _rx.closeChar!.Value;

                    if (ordinal)
                    {
                        // ---- CHAR + Ordinal (Span içeride) ----
                        ReadOnlySpan<char> hay = _input.AsSpan();
                        ReadOnlySpan<char> must = _rx.requiredContains.AsSpanOrEmpty();

                        while (true)
                        {
                            if (oc == cc) // toggle çifti
                            {
                                int s = FindNextToken_Ordinal(hay, oc, _pos, _rx.escapeChar);
                                if (s < 0) return false;

                                int from = s + 1;
                                int e = FindNextToken_Ordinal(hay, cc, from, _rx.escapeChar);
                                if (e < 0) return false;

                                var content = hay.Slice(from, e - from);
                                if (PassesOrdinal(content, must, _rx.excludedContains))
                                {
                                    int outStart = _rx.includeBounds ? s : from;
                                    int outLen = _rx.includeBounds ? (e - s + 1) : content.Length;

                                    match = new LiteMatch(outStart, outLen, _input.AsMemory(outStart, outLen));
                                    _pos = e + 1;
                                    _remaining--;
                                    return true;
                                }

                                _pos = e + 1;
                                continue;
                            }
                            else
                            {
                                int s = FindNextToken_Ordinal(hay, oc, _pos, _rx.escapeChar);
                                if (s < 0) return false;

                                int from = s + 1;
                                int e;

                                if (_rx.allowNesting)
                                {
                                    e = FindBalancedEnd_Ordinal(hay, from, oc, cc, _rx.escapeChar);
                                    if (e < 0) return false;
                                }
                                else if (!_rx.longest)
                                {
                                    e = FindNextToken_Ordinal(hay, cc, from, _rx.escapeChar);
                                    if (e < 0) return false;
                                }
                                else
                                {
                                    int nextStart = FindNextToken_Ordinal(hay, oc, from, _rx.escapeChar);
                                    int searchEnd = nextStart < 0 ? hay.Length : nextStart;
                                    e = FindLastTokenBefore_Ordinal(hay, cc, from, searchEnd, _rx.escapeChar);
                                    if (e < 0) return false;
                                }

                                var content = hay.Slice(from, e - from);
                                if (PassesOrdinal(content, must, _rx.excludedContains))
                                {
                                    int outStart = _rx.includeBounds ? s : from;
                                    int outLen = _rx.includeBounds ? (e - s + 1) : content.Length;

                                    match = new LiteMatch(outStart, outLen, _input.AsMemory(outStart, outLen));
                                    _pos = e + 1;
                                    _remaining--;
                                    return true;
                                }

                                _pos = e + 1;
                                continue;
                            }
                        }
                    }
                    else
                    {
                        // ---- CHAR + Culture/IgnoreCase (string fallback) ----
                        string sTok = _rx.open;
                        string eTok = _rx.close;

                        while (true)
                        {
                            if (oc == cc) // toggle
                            {
                                int s = FindNextToken_String(_input, sTok, _pos, _rx.comparison, _rx.escapeChar);
                                if (s < 0) return false;

                                int from = s + sTok.Length;
                                int e = FindNextToken_String(_input, eTok, from, _rx.comparison, _rx.escapeChar);
                                if (e < 0) return false;

                                if (PassesWithComparison(_input, from, e - from, _rx.requiredContains, _rx.excludedContains, _rx.comparison))
                                {
                                    int outStart = _rx.includeBounds ? s : from;
                                    int outLen = _rx.includeBounds ? (e + eTok.Length - s) : (e - from);

                                    match = new LiteMatch(outStart, outLen, _input.AsMemory(outStart, outLen));
                                    _pos = e + eTok.Length;
                                    _remaining--;
                                    return true;
                                }

                                _pos = e + eTok.Length;
                                continue;
                            }
                            else
                            {
                                int s = FindNextToken_String(_input, sTok, _pos, _rx.comparison, _rx.escapeChar);
                                if (s < 0) return false;

                                int from = s + sTok.Length;
                                int e;

                                if (_rx.allowNesting)
                                {
                                    e = FindBalancedEnd_String(_input, from, sTok, eTok, _rx.comparison, _rx.escapeChar);
                                    if (e < 0) return false;
                                }
                                else if (!_rx.longest)
                                {
                                    e = FindNextToken_String(_input, eTok, from, _rx.comparison, _rx.escapeChar);
                                    if (e < 0) return false;
                                }
                                else
                                {
                                    int nextStart = FindNextToken_String(_input, sTok, from, _rx.comparison, _rx.escapeChar);
                                    int searchEnd = nextStart < 0 ? _input.Length : nextStart;
                                    e = FindLastTokenBefore_String(_input, eTok, from, searchEnd, _rx.comparison, _rx.escapeChar);
                                    if (e < 0) return false;
                                }

                                if (PassesWithComparison(_input, from, e - from, _rx.requiredContains, _rx.excludedContains, _rx.comparison))
                                {
                                    int outStart = _rx.includeBounds ? s : from;
                                    int outLen = _rx.includeBounds ? (e + eTok.Length - s) : (e - from);

                                    match = new LiteMatch(outStart, outLen, _input.AsMemory(outStart, outLen));
                                    _pos = e + eTok.Length;
                                    _remaining--;
                                    return true;
                                }

                                _pos = e + eTok.Length;
                                continue;
                            }
                        }
                    }
                }
                else
                {
                    // ---- STRING tokenlar ----
                    if (ordinal)
                    {
                        ReadOnlySpan<char> hay = _input.AsSpan();
                        ReadOnlySpan<char> sdl = _rx.open.AsSpan();
                        ReadOnlySpan<char> edl = _rx.close.AsSpan();
                        ReadOnlySpan<char> must = _rx.requiredContains.AsSpanOrEmpty();

                        while (true)
                        {
                            if (sdl.SequenceEqual(edl)) // toggle
                            {
                                int s = FindNextToken_Ordinal(hay, sdl, _pos, _rx.escapeChar);
                                if (s < 0) return false;

                                int from = s + sdl.Length;
                                int e = FindNextToken_Ordinal(hay, edl, from, _rx.escapeChar);
                                if (e < 0) return false;

                                var content = hay.Slice(from, e - from);
                                if (PassesOrdinal(content, must, _rx.excludedContains))
                                {
                                    int outStart = _rx.includeBounds ? s : from;
                                    int outLen = _rx.includeBounds ? (e + edl.Length - s) : content.Length;

                                    match = new LiteMatch(outStart, outLen, _input.AsMemory(outStart, outLen));
                                    _pos = e + edl.Length;
                                    _remaining--;
                                    return true;
                                }

                                _pos = e + edl.Length;
                                continue;
                            }
                            else
                            {
                                int s = FindNextToken_Ordinal(hay, sdl, _pos, _rx.escapeChar);
                                if (s < 0) return false;

                                int from = s + sdl.Length;
                                int e;

                                if (_rx.allowNesting)
                                {
                                    e = FindBalancedEnd_Ordinal(hay, from, sdl, edl, _rx.escapeChar);
                                    if (e < 0) return false;
                                }
                                else if (!_rx.longest)
                                {
                                    e = FindNextToken_Ordinal(hay, edl, from, _rx.escapeChar);
                                    if (e < 0) return false;
                                }
                                else
                                {
                                    int nextStart = FindNextToken_Ordinal(hay, sdl, from, _rx.escapeChar);
                                    int searchEnd = nextStart < 0 ? hay.Length : nextStart;
                                    e = FindLastTokenBefore_Ordinal(hay, edl, from, searchEnd, _rx.escapeChar);
                                    if (e < 0) return false;
                                }

                                var content = hay.Slice(from, e - from);
                                if (PassesOrdinal(content, must, _rx.excludedContains))
                                {
                                    int outStart = _rx.includeBounds ? s : from;
                                    int outLen = _rx.includeBounds ? (e + edl.Length - s) : content.Length;

                                    match = new LiteMatch(outStart, outLen, _input.AsMemory(outStart, outLen));
                                    _pos = e + edl.Length;
                                    _remaining--;
                                    return true;
                                }

                                _pos = e + edl.Length;
                                continue;
                            }
                        }
                    }
                    else
                    {
                        while (true)
                        {
                            if (string.Equals(_rx.open, _rx.close, _rx.comparison)) // toggle
                            {
                                int s = FindNextToken_String(_input, _rx.open, _pos, _rx.comparison, _rx.escapeChar);
                                if (s < 0) return false;

                                int from = s + _rx.open.Length;
                                int e = FindNextToken_String(_input, _rx.close, from, _rx.comparison, _rx.escapeChar);
                                if (e < 0) return false;

                                if (PassesWithComparison(_input, from, e - from, _rx.requiredContains, _rx.excludedContains, _rx.comparison))
                                {
                                    int outStart = _rx.includeBounds ? s : from;
                                    int outLen = _rx.includeBounds ? (e + _rx.close.Length - s) : (e - from);

                                    match = new LiteMatch(outStart, outLen, _input.AsMemory(outStart, outLen));
                                    _pos = e + _rx.close.Length;
                                    _remaining--;
                                    return true;
                                }

                                _pos = e + _rx.close.Length;
                                continue;
                            }
                            else
                            {
                                int s = FindNextToken_String(_input, _rx.open, _pos, _rx.comparison, _rx.escapeChar);
                                if (s < 0) return false;

                                int from = s + _rx.open.Length;
                                int e;

                                if (_rx.allowNesting)
                                {
                                    e = FindBalancedEnd_String(_input, from, _rx.open, _rx.close, _rx.comparison, _rx.escapeChar);
                                    if (e < 0) return false;
                                }
                                else if (!_rx.longest)
                                {
                                    e = FindNextToken_String(_input, _rx.close, from, _rx.comparison, _rx.escapeChar);
                                    if (e < 0) return false;
                                }
                                else
                                {
                                    int nextStart = FindNextToken_String(_input, _rx.open, from, _rx.comparison, _rx.escapeChar);
                                    int searchEnd = nextStart < 0 ? _input.Length : nextStart;
                                    e = FindLastTokenBefore_String(_input, _rx.close, from, searchEnd, _rx.comparison, _rx.escapeChar);
                                    if (e < 0) return false;
                                }

                                if (PassesWithComparison(_input, from, e - from, _rx.requiredContains, _rx.excludedContains, _rx.comparison))
                                {
                                    int outStart = _rx.includeBounds ? s : from;
                                    int outLen = _rx.includeBounds ? (e + _rx.close.Length - s) : (e - from);

                                    match = new LiteMatch(outStart, outLen, _input.AsMemory(outStart, outLen));
                                    _pos = e + _rx.close.Length;
                                    _remaining--;
                                    return true;
                                }

                                _pos = e + _rx.close.Length;
                                continue;
                            }
                        }
                    }
                }
            }
        }

        // ---------------- Span Ordinal helpers (Scanner içinde çağrılır) ----------------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FindNextToken_Ordinal(ReadOnlySpan<char> hay, char tok, int start, char? esc)
        {
            while (true)
            {
                int rel = hay[start..].IndexOf(tok);
                if (rel < 0) return -1;
                int idx = start + rel;
                if (!IsEscaped(hay, idx, esc)) return idx;
                start = idx + 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FindNextToken_Ordinal(ReadOnlySpan<char> hay, ReadOnlySpan<char> tok, int start, char? esc)
        {
            while (true)
            {
                int rel = hay[start..].IndexOf(tok);
                if (rel < 0) return -1;
                int idx = start + rel;
                if (!IsEscaped(hay, idx, esc)) return idx;
                start = idx + 1;
            }
        }

        private static int FindLastTokenBefore_Ordinal(ReadOnlySpan<char> hay, char tok, int start, int endExclusive, char? esc)
        {
            int last = -1;
            int pos = start;
            while (true)
            {
                int rel = hay[pos..endExclusive].IndexOf(tok);
                if (rel < 0) break;
                int idx = pos + rel;
                if (!IsEscaped(hay, idx, esc)) last = idx;
                pos = idx + 1;
            }
            return last;
        }

        private static int FindLastTokenBefore_Ordinal(ReadOnlySpan<char> hay, ReadOnlySpan<char> tok, int start, int endExclusive, char? esc)
        {
            int last = -1;
            int pos = start;
            while (true)
            {
                int rel = hay[pos..endExclusive].IndexOf(tok);
                if (rel < 0) break;
                int idx = pos + rel;
                if (!IsEscaped(hay, idx, esc)) last = idx;
                pos = idx + 1;
            }
            return last;
        }

        private static int FindBalancedEnd_Ordinal(ReadOnlySpan<char> hay, int from, char oc, char cc, char? esc)
        {
            int depth = 1;
            int pos = from;
            while (true)
            {
                int nOpen = FindNextToken_Ordinal(hay, oc, pos, esc);
                int nClose = FindNextToken_Ordinal(hay, cc, pos, esc);
                if (nClose < 0) return -1;
                if (nOpen >= 0 && nOpen < nClose) { depth++; pos = nOpen + 1; }
                else { depth--; if (depth == 0) return nClose; pos = nClose + 1; }
            }
        }

        private static int FindBalancedEnd_Ordinal(ReadOnlySpan<char> hay, int from, ReadOnlySpan<char> sdl, ReadOnlySpan<char> edl, char? esc)
        {
            int depth = 1;
            int pos = from;
            while (true)
            {
                int nOpen = FindNextToken_Ordinal(hay, sdl, pos, esc);
                int nClose = FindNextToken_Ordinal(hay, edl, pos, esc);
                if (nClose < 0) return -1;
                if (nOpen >= 0 && nOpen < nClose) { depth++; pos = nOpen + sdl.Length; }
                else { depth--; if (depth == 0) return nClose; pos = nClose + edl.Length; }
            }
        }

        // ---------------- String fallback helpers ----------------
        private static int FindNextToken_String(string s, string tok, int start, StringComparison cmp, char? esc)
        {
            while (true)
            {
                int idx = s.IndexOf(tok, start, s.Length - start, cmp);
                if (idx < 0) return -1;
                if (!IsEscaped(s, idx, esc)) return idx;
                start = idx + 1;
            }
        }

        private static int FindLastTokenBefore_String(string s, string tok, int start, int endExclusive, StringComparison cmp, char? esc)
        {
            int last = -1;
            int pos = start;
            while (true)
            {
                int idx = s.IndexOf(tok, pos, endExclusive - pos, cmp);
                if (idx < 0) break;
                if (!IsEscaped(s, idx, esc)) last = idx;
                pos = idx + 1;
            }
            return last;
        }

        private static int FindBalancedEnd_String(string s, int from, string sTok, string eTok, StringComparison cmp, char? esc)
        {
            int depth = 1;
            int pos = from;
            while (true)
            {
                int nOpen = FindNextToken_String(s, sTok, pos, cmp, esc);
                int nClose = FindNextToken_String(s, eTok, pos, cmp, esc);
                if (nClose < 0) return -1;
                if (nOpen >= 0 && nOpen < nClose) { depth++; pos = nOpen + sTok.Length; }
                else { depth--; if (depth == 0) return nClose; pos = nClose + eTok.Length; }
            }
        }

        // ---------------- Filters & escape ----------------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool PassesOrdinal(ReadOnlySpan<char> content, ReadOnlySpan<char> must, IReadOnlyList<string> mustNot)
        {
            if (!must.IsEmpty && content.IndexOf(must) < 0) return false;
            if (mustNot != null)
            {
                for (int i = 0; i < mustNot.Count; i++)
                {
                    var bad = mustNot[i];
                    if (!string.IsNullOrEmpty(bad) && content.IndexOf(bad.AsSpan()) >= 0) return false;
                }
            }
            return true;
        }

        private static bool PassesWithComparison(
            string input, int start, int length,
            string mustContain,
            IReadOnlyList<string> mustNotContain,
            StringComparison cmp)
        {
            if (!string.IsNullOrEmpty(mustContain) &&
                input.IndexOf(mustContain, start, length, cmp) < 0)
                return false;

            if (mustNotContain != null)
            {
                for (int i = 0; i < mustNotContain.Count; i++)
                {
                    var bad = mustNotContain[i];
                    if (!string.IsNullOrEmpty(bad) &&
                        input.IndexOf(bad, start, length, cmp) >= 0)
                        return false;
                }
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsEscaped(ReadOnlySpan<char> src, int tokenIndex, char? esc)
        {
            if (esc is null) return false;
            char e = esc.Value;
            int b = tokenIndex - 1;
            int streak = 0;
            while (b >= 0 && src[b] == e) { streak++; b--; }
            return (streak & 1) == 1; // tekse kaçışlı
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsEscaped(string src, int tokenIndex, char? esc)
        {
            if (esc is null) return false;
            char e = esc.Value;
            int b = tokenIndex - 1;
            int streak = 0;
            while (b >= 0 && src[b] == e) { streak++; b--; }
            return (streak & 1) == 1;
        }
    }

    // küçük helper
    public static partial class RegexLiteHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<char> AsSpanOrEmpty(this string s)
            => s is null ? ReadOnlySpan<char>.Empty : s.AsSpan();

        public static LiteMatch Match(
           this string input, string open, string close,
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

        public static LiteMatch Match(
           this string input, char open, char close,
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

        public static List<LiteMatch> Matches(
           this string input, string open, string close,
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

        public static List<LiteMatch> Matches(
           this string input, char open, char close,
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
           this string input, string open, string close,
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
           this string input, char open, char close,
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

        public static string Replace(
           this string input, string open, string close, string replacement,
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

        public static string Replace(
           this string input, char open, char close, string replacement,
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

        public static string Replace(
          this string input, string open, string close, Func<LiteMatch, string> evaluator,
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

        public static string Replace(
          this string input, char open, char close, Func<LiteMatch, string> evaluator,
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
