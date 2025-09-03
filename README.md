# RegexLite

**RegexLite**, “başlangıç–bitiş belirteçleri” ile çalışan, **Regex gerektirmeyen**, **span tabanlı** ve **allocation-minimal** bir metin çıkarıcıdır.  
.NET 6+ uyumludur, içerde **ReadOnlySpan<char>** ile tarar; sonuçları **ReadOnlyMemory<char>** olarak döndürür (kopyasız işleyip gerekirse `.ToString()` ile string’e çevirebilirsin).

> Hedef: Regex’in ifade gücüne ihtiyaç olmayan, **`open/close + içerir / içermez`** kurallı senaryolarda **basit, hızlı ve öngörülebilir** bir alternatif.

---

## Özellikler

- **Açık/Kapalı belirteçleri:** `open` & `close` **char** veya **string** olabilir (ör. `(` … `)`, `<div` … `</div>`).
- **Zorunlu içerik / hariç içerik** filtreleri: `requiredContains`, `excludedContains`.
- **Dahil edilecek sınırlar:** `includeBounds` ile open/close’u çıktıya kat/çıkar.
- **En kısa vs. en uzun eşleşme:** `longest` (non-greedy yerine isim netliği).
- **Dengeli (nested) eşleşme:** `allowNesting` ile iç içe parantez yapıları.
- **Kaçış karakteri:** `escapeChar` (ör. `\<div` gerçek açılış sayılmasın).
- **Karşılaştırma modu:** `comparison` (`Ordinal` varsayılan; *IgnoreCase/kültür* desteği var).
- **Akış kontrolü:** `startAt`, `maxMatches`, `Replace` (sabit veya evaluator).
- **Düşük GC:** Sonuçlar `ReadOnlyMemory<char>`; yalnızca gerçekten string’e çevirdiğinde allocation olur.

---

## Kurulum

Bu proje tek dosyalık bir yardımcıdır.

1. `RegexLite.cs` dosyasını projenize ekleyin.
2. Hedef çerçeve: **.NET 6+**  
3. NuGet gerekmez (ileride paketlenebilir).

---

## Hızlı Başlangıç

```csharp
// id= içeren <div ... </div> bloklarını çıkar, hidden/disabled geçenleri ele
var rx = new RegexLite(
    open: "<div",
    close: "</div>",
    requiredContains: "id=",
    excludedContains: new[] { "hidden", "disabled" },
    includeBounds: true,         // <div ... </div> dahil
    longest: false,              // non-greedy
    allowNesting: false,
    escapeChar: '\\',
    comparison: StringComparison.Ordinal);

// İlk eşleşme (Regex.Match gibi)
var m = rx.Match(html);
if (m.Success)
{
    // m.Memory → ReadOnlyMemory<char>
    // m.Span   → ReadOnlySpan<char> (allocation yok)
    Console.WriteLine(m.Value);  // string'e çevirmek istersen (allocate eder)
}

// Tüm eşleşmeler (Regex.Matches gibi)
foreach (var mm in rx.Matches(html))
{
    // Kopyasız işleme:
    var span = mm.Span;
}

// Replace — sabit metin
string blocked = rx.Replace(html, "[BLOCK]");

// Replace — evaluator
string tagged = rx.Replace(html, mm => $"[{mm.Value}]");
```

---

## API

### Sınıf: `RegexLite`

#### Kurucular

```csharp
// String belirteçler
public RegexLite(
    string open,
    string close,
    string? requiredContains = null,
    IReadOnlyList<string>? excludedContains = null,
    bool includeBounds = false,
    bool longest = false,
    bool allowNesting = false,
    char? escapeChar = null,
    StringComparison comparison = StringComparison.Ordinal);

// Char belirteçler
public RegexLite(
    char open,
    char close,
    string? requiredContains = null,
    IReadOnlyList<string>? excludedContains = null,
    bool includeBounds = false,
    bool longest = false,
    bool allowNesting = false,
    char? escapeChar = null,
    StringComparison comparison = StringComparison.Ordinal);
```

#### Metotlar

```csharp
// İlk eşleşme
LiteMatch Match(string input, int startAt = 0);

// Tüm eşleşmeler (liste)
List<LiteMatch> Matches(string input, int startAt = 0, int maxMatches = int.MaxValue);

// Varlık kontrolü
bool IsMatch(string input, int startAt = 0);

// Değiştir — sabit replacement
string Replace(string input, string replacement, int startAt = 0, int maxReplacements = int.MaxValue);

// Değiştir — evaluator (Regex.Replace(input, m => ...))
string Replace(string input, Func<LiteMatch, string> evaluator, int startAt = 0, int maxReplacements = int.MaxValue);
```

#### Statik kısa yollar

```csharp
// Regex.Match / Matches / IsMatch / Replace benzerleri (tek seferlik kullanım için)
static LiteMatch Match(...);
static List<LiteMatch> Matches(...);
static bool IsMatch(...);
static string Replace(... string replacement ...);
static string Replace(... Func<LiteMatch, string> evaluator ...);
```

#### Sonuç tipi: `RegexLite.LiteMatch`

```csharp
public readonly struct LiteMatch
{
    public int Index { get; }
    public int Length { get; }
    public int End => Index + Length;
    public ReadOnlyMemory<char> Memory { get; } // kopyasız
    public ReadOnlySpan<char>   Span   { get; } // Memory.Span
    public string Value { get; }                // ToString() (allocation)
    public bool Success { get; }
}
```

---

## Parametreler — Açıklamalar ve Örnekler

### `requiredContains : string`
Açık–kapalı aralığında **mutlaka geçmesi gereken** alt dize.  
Boş/null ⇒ zorunlu yok.

```csharp
var rx = new RegexLite("<div", "</div>", requiredContains: "id=");
rx.Matches(@"<div id=\"a\">ok</div><div class=\"hidden\">x</div>");
```

### `excludedContains : IReadOnlyList<string>`
Açık–kapalı aralığında **geçmemesi gereken** alt dizeler. Listedekilerden biri görülürse eşleşme elenir.

```csharp
var rx = new RegexLite("<div", "</div>", requiredContains: "id=", excludedContains: new[] { "hidden", "disabled" });
```

### `includeBounds : bool`
Sonuçta `open/close` sınırlarını **dahil et**.

```csharp
new RegexLite("(", ")", includeBounds:false).Match("x=(1+2)").Value == "1+2";
new RegexLite("(", ")", includeBounds:true ).Match("x=(1+2)").Value == "(1+2)";
```

### `longest : bool`
Aynı başlangıçtan, bir sonraki `open`e kadar **son `close`** (en uzun) ile eşle.  
Varsayılan **false** (non-greedy, ilk `close`).

```csharp
string s = "<a>1</a> ... </a>";
new RegexLite("<a>", "</a>", longest:false, includeBounds:true).Match(s).Value == "<a>1</a>";
new RegexLite("<a>", "</a>", longest:true,  includeBounds:true).Match(s).Value == "<a>1</a> ... </a>";
```

> `allowNesting:true` ise öncelik dengeli kapanış bulmaktır.

### `allowNesting : bool`
Dengeli (nested) yapıları destekle.

```csharp
var rx = new RegexLite("(", ")", allowNesting:true);
rx.Match("a(b(c)d)e").Value == "b(c)d";
```

### `escapeChar : char?`
Bu karakterle kaçışlanan `open/close` **sayılmaz**.

```csharp
string s = @"name=\"John\"\"\ \" age\"";
var rx = new RegexLite("\"", "\"", escapeChar: '\\', includeBounds:false);
// Eşleşmeler: John\"  ve  " + " age
```

### `comparison : StringComparison`
Karşılaştırma modu (büyük/küçük harf, kültür).

```csharp
string s = "<DIV id='x'></DIV>";
new RegexLite("<div", "</div>", comparison: StringComparison.Ordinal).IsMatch(s)           == false;
new RegexLite("<div", "</div>", comparison: StringComparison.OrdinalIgnoreCase).IsMatch(s) == true;
```

### `startAt : int` & `maxMatches : int`
Aramaya **başlangıç indeksi** ve **eşleşme sayısı sınırı**.

```csharp
var rx = new RegexLite("<p>", "</p>", includeBounds:true);
// İlk 10 eşleşmeyi 1000. indexten sonra ara
var list = rx.Matches(html, startAt: 1000, maxMatches: 10);
```

---

## Ek Örnekler

### Toggle modu (`open == close`)
Aynı belirteç hem açılış hem kapanış ise (ör. `"`), çiftler halinde yakalanır:

```csharp
var rx = new RegexLite("\"", "\"", includeBounds:false);
foreach (var m in rx.Matches(@"x=\"a b\" y=\"c\""))
    Console.WriteLine(m.Value); // a b  ve  c
```

### Replace ile sanitize
```csharp
var rx = new RegexLite("<script", "</script>", longest:true, includeBounds:true, comparison:StringComparison.OrdinalIgnoreCase);
string sanitized = rx.Replace(html, "[[SCRIPT_REMOVED]]");
```

---

## Performans

- **O(n)** tek geçiş; Regex motoru, state machine ve backtracking yok.  
- **Span tabanlı** tarama (Ordinal) SIMD hızlandırmalı; filtreler de kopyasız.  
- Sonuçlar `ReadOnlyMemory<char>` → **minimum allocation**.

### BenchmarkDotNet iskeleti

> Tahmini sonuç: RegexLite, .NET 6 Regex’e göre genellikle **2–10×** hızlı ve çok daha az allocation yapar (özellikle `Ordinal` + kısa tokenlar).

```csharp
// <Project Sdk="Microsoft.NET.Sdk">
// <PropertyGroup><TargetFramework>net6.0</TargetFramework><Optimize>true</Optimize></PropertyGroup>
// <ItemGroup><PackageReference Include="BenchmarkDotNet" Version="0.13.12" /></ItemGroup>

using System;
using System.Text;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

[MemoryDiagnoser]
public class DelimVsRegexBench
{
    private string _text;
    private Regex _rxInterp;
    private Regex _rxCompiled;
    private RegexLite _lite;

    [GlobalSetup]
    public void Setup()
    {
        var sb = new StringBuilder(2_000_000);
        for (int i = 0; i < 20000; i++)
        {
            sb.Append("<div id=\"a\">ok</div>");
            sb.Append("<div class=\"hidden\">no</div>");
            sb.Append("<span>skip</span>");
            sb.Append("<div id=\"b\">ok</div>");
            if ((i % 7) == 0) sb.Append('\n');
        }
        _text = sb.ToString();

        string pattern = $"{Regex.Escape("<div")}(.*?){Regex.Escape("</div>")}";
        var opts = RegexOptions.Singleline;

        _rxInterp   = new Regex(pattern, opts);
        _rxCompiled = new Regex(pattern, opts | RegexOptions.Compiled);

        _lite = new RegexLite("<div", "</div>",
                              requiredContains: "id=",
                              excludedContains: new[] { "hidden" },
                              includeBounds: true,
                              longest: false,
                              comparison: StringComparison.Ordinal);
    }

    [Benchmark(Baseline = true)]
    public int RegexLite_Matches()
    {
        int count = 0;
        foreach (var m in _lite.Matches(_text)) count++;
        return count;
    }

    [Benchmark]
    public int Regex_Interpreted_PostFilter()
    {
        int count = 0;
        var mc = _rxInterp.Matches(_text);
        foreach (Match m in mc)
        {
            var content = m.Groups[1].Value;
            if (content.Contains("id=", StringComparison.Ordinal) &&
                !content.Contains("hidden", StringComparison.Ordinal))
                count++;
        }
        return count;
    }

    [Benchmark]
    public int Regex_Compiled_PostFilter()
    {
        int count = 0;
        var mc = _rxCompiled.Matches(_text);
        foreach (Match m in mc)
        {
            var content = m.Groups[1].Value;
            if (content.Contains("id=", StringComparison.Ordinal) &&
                !content.Contains("hidden", StringComparison.Ordinal))
                count++;
        }
        return count;
    }

    public static void Main(string[] args) => BenchmarkRunner.Run<DelimVsRegexBench>();
}
```

---

## Tasarım Notları

- **Span içeride / iterator dışarıda:** `ReadOnlySpan<char>` **ref struct** olduğu için iterator içinde kullanılamaz. Bu yüzden tarama **Scanner** içinde yapılır; dışarıya `LiteMatch` üretir.
- **Culture/IgnoreCase fallback:** `Ordinal` modda span/`MemoryExtensions.IndexOf`; diğer modlarda **`string.IndexOf(..., StringComparison)`** (allocation yapmaz).
- **Nesting & longest:** `allowNesting:true` iken derinlik sayacı dengelenene kadar devam eder; `longest` devrede ise yalnızca nesting **kapalı** iken anlamlıdır.
- **Bellek:** `ReadOnlyMemory<char>` orijinal `input`’ı yaşatır. Sonuçları uzun süre saklayacaksan `m.Value` ile bağımsızlaştır.

---

## Sınırlamalar

- Lookahead/backreference, capture group gibi gelişmiş Regex özellikleri **yok** (bilinçli olarak basit tutuldu).
- `open == close` (toggle) modunda **nesting** anlamsızdır (devre dışı gibi düşün).
- Overlap desteklenmez (her eşleşme `close` sonrası başlar).

---

## Yol Haritası

- `IEnumerable<LiteMatch>` döndüren gerçek **streaming** API (liste oluşturmadan tüketim) — *(Replace/Match akışsal, Matches listeliyor)*  
- NuGet paketi  
- Özel hata raporlama (eşleşme bulunamadı/erken bitiş vs. için diagnostic)

---

## Katkı

PR’ler ve issue’lar memnuniyetle. Kod stili: **.NET 6**, `AggressiveInlining` minimal, public API’de isim ve seçenekler **stabil**.

---

## Lisans

MIT (değiştirebilirsiniz).

---

## Teşekkür

Bu kütüphane, gerçek hayattaki “regex’e gerek yok, daha hızlı/öngörülebilir olsun” senaryolarına odaklı bir **lightweight extractor**. Öneri ve geri bildirimler için issue açabilirsiniz.

