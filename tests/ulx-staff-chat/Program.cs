using System.Globalization;
using LifePunch.DXRP.Addons.StaffMenu;

var cases = new (string? Draft, int Max, bool Allowed, string? Expected)[]
{
	(null, 150, false, null),
	("", 150, false, null),
	(" \t\r\n", 150, false, null),
	("/", 150, false, null),
	("/freeze Fred", 150, false, null),
	("//hello", 150, false, null),
	("// /freeze Fred", 150, false, null),
	("/f faction message", 150, false, null),
	("/F faction message", 150, false, null),
	("@help", 150, false, null),
	(" \t /freeze Fred", 150, false, null),
	("\r\n@help", 150, false, null),
	("\u00a0//global", 150, false, null),
	("\u2003/f faction", 150, false, null),
	("\u2028/ban Fred", 150, false, null),
	("\u200b/ban Fred", 150, false, null),
	("\ufeff@help", 150, false, null),
	("\0/f faction", 150, false, null),
	("\u0001/plain", 150, false, null),
	("hello\n/ban Fred", 150, false, null),
	("hello\r@help", 150, false, null),
	("hello\tworld", 150, false, null),
	("hello\u001fworld", 150, false, null),
	("hello\u2028world", 150, false, null),
	("hello\u2029world", 150, false, null),
	("hello\0world", 150, false, null),
	("  hello staff  ", 150, true, "hello staff"),
	("\nhello staff\n", 150, true, "hello staff"),
	("!hello staff", 150, true, "!hello staff"),
	("hello /freeze is text", 150, true, "hello /freeze is text"),
	("hello @staff", 150, true, "hello @staff"),
	("<b>ordinary text</b>", 150, true, "<b>ordinary text</b>"),
	("joined \ud83d\udc69\u200d\ud83d\udcbb emoji", 150, true, "joined \ud83d\udc69\u200d\ud83d\udcbb emoji"),
	("12345", 5, true, "12345"),
	(" 12345 ", 5, true, "12345"),
	("123456", 5, false, null),
	("hello", 0, false, null),
	("hello", -1, false, null)
};

var assertions = 0;
foreach (var test in cases)
{
	var allowed = StaffChatInput.TryPrepare(test.Draft, test.Max, out var prepared, out var reason);
	Require(allowed == test.Allowed, $"Unexpected decision for case {assertions + 1}.");
	Require(allowed ? prepared == test.Expected : !string.IsNullOrEmpty(reason), "Output or error message mismatch.");
	if (allowed) Require(!ReachesNativeCommandOrChannelRoute(prepared), "Accepted text entered native command or channel routing.");
	assertions++;
}

// Native Trim recognizes Unicode whitespace; every such prefix and every control/format
// prefix must fail closed ahead of either command introducer. This exercises the actual helper.
var prefixCases = 0;
for (var code = 0; code <= char.MaxValue; code++)
{
	var prefix = (char)code;
	if (!char.IsWhiteSpace(prefix) && !char.IsControl(prefix)
		&& char.GetUnicodeCategory(prefix) != UnicodeCategory.Format) continue;
	foreach (var command in new[] { "/freeze Fred", "//public", "/f faction", "@ticket" })
	{
		Require(!StaffChatInput.TryPrepare(prefix + command, 150, out _, out _), $"Prefix U+{code:X4} bypassed guard.");
		prefixCases++;
	}
}

Console.WriteLine($"PASS: {cases.Length} explicit cases and {prefixCases} Unicode command-prefix cases against the actual StaffChatInput helper.");

var emptyFeed = StaffChatRecent.Select(Array.Empty<(int Sequence, bool Staff)>(), entry => entry.Staff, Project);
Require(emptyFeed.Available == 0 && emptyFeed.Messages.Count == 0, "Empty feed claimed messages.");

// Native enumeration is newest first. Twenty unrelated messages must not consume staff slots.
var received = Enumerable.Range(0, 80).Select(index => (Sequence: 80 - index, Staff: index >= 20)).ToList();
var projected = 0;
var mixedFeed = StaffChatRecent.Select(received, entry => entry.Staff, entry => { projected++; return Project(entry); });
Require(mixedFeed.Available == 60, "Available count included unrelated channels or used the display cap.");
Require(mixedFeed.Messages.Count == 40 && projected == 40, "Display or projection exceeded the Observe cap.");
Require(mixedFeed.Messages[0].Text == "60" && mixedFeed.Messages[^1].Text == "21", "Native newest-first order was lost.");
Require(received.Count == 80, "Projection modified the shared source.");

var smallFeed = StaffChatRecent.Select(new[] { (Sequence: 3, Staff: true), (Sequence: 2, Staff: false), (Sequence: 1, Staff: true) }, entry => entry.Staff, Project);
Require(smallFeed.Available == 2 && smallFeed.Messages.Count == 2, "Below-cap denominator was fabricated.");
Require(smallFeed.Messages[0].Text == "3" && smallFeed.Messages[1].Text == "1", "Filtering changed retained order.");

var unrelatedFeed = StaffChatRecent.Select(new[] { (Sequence: 2, Staff: false), (Sequence: 1, Staff: false) }, entry => entry.Staff, Project);
Require(unrelatedFeed.Available == 0 && unrelatedFeed.Messages.Count == 0, "An unrelated channel entered staff chat.");

var capFeed = StaffChatRecent.Select(Enumerable.Range(1, 40).Select(value => (Sequence: value, Staff: true)), entry => entry.Staff, Project);
Require(capFeed.Available == 40 && capFeed.Messages.Count == 40, "Exact-cap boundary lost a message.");

// A later smaller native buffer must shrink the denominator; no historical total is retained.
received.RemoveRange(0, 78);
var trimmedFeed = StaffChatRecent.Select(received, entry => entry.Staff, Project);
Require(trimmedFeed.Available == 2 && trimmedFeed.Messages.Count == 2, "Evicted messages remained in a later snapshot.");
Require(StaffChatHost.ReadMessages(default).Available == 0, "Local build fabricated native chat history.");
Console.WriteLine("PASS: recent-message empty, unrelated-channel, interleaved, capped, exact-boundary, source-preservation and eviction cases against the actual selection helper; local fallback remains empty.");

static StaffChatMessage Project((int Sequence, bool Staff) entry)
	=> new(Guid.Empty, "Native author", "#ffffff", string.Empty, string.Empty, entry.Sequence.ToString(CultureInfo.InvariantCulture));

static void Require(bool condition, string message)
{
	if (!condition) throw new InvalidOperationException(message);
}

// This classifier records the source-inspected native parser boundary, not command execution.
// SubmitPlayerChat checks // and local /; ProcessPlayerChat checks /f and trims before / or @.
static bool ReachesNativeCommandOrChannelRoute(string text)
{
	if (text.StartsWith("//", StringComparison.Ordinal) || text.StartsWith('/')) return true;
	if (text.StartsWith("/f ", StringComparison.OrdinalIgnoreCase)) return true;
	var hostText = text.Trim();
	return hostText.StartsWith('/') || hostText.StartsWith('@');
}
