namespace Markdowner.Rendering;

/// <summary>
/// A small shortcode table so <c>:tada:</c> previews as an actual emoji rather
/// than as literal text. Deliberately partial — anything unknown falls back to
/// showing the shortcode, which is also what an unknown code does on Discord.
/// </summary>
public static class EmojiMap
{
    public static bool TryGet(string shortcode, out string emoji) =>
        Table.TryGetValue(shortcode.ToLowerInvariant(), out emoji!);

    private static readonly Dictionary<string, string> Table = new(StringComparer.Ordinal)
    {
        ["smile"] = "😄", ["smiley"] = "😃", ["grin"] = "😁", ["laughing"] = "😆",
        ["joy"] = "😂", ["rofl"] = "🤣", ["wink"] = "😉", ["blush"] = "😊",
        ["heart_eyes"] = "😍", ["thinking"] = "🤔", ["neutral_face"] = "😐",
        ["confused"] = "😕", ["cry"] = "😢", ["sob"] = "😭", ["rage"] = "😡",
        ["sunglasses"] = "😎", ["shushing_face"] = "🤫", ["upside_down_face"] = "🙃",
        ["sweat_smile"] = "😅", ["yawning_face"] = "🥱", ["exploding_head"] = "🤯",
        ["skull"] = "💀", ["ghost"] = "👻", ["alien"] = "👽", ["robot"] = "🤖",

        ["thumbsup"] = "👍", ["+1"] = "👍", ["thumbsdown"] = "👎", ["-1"] = "👎",
        ["ok_hand"] = "👌", ["clap"] = "👏", ["pray"] = "🙏", ["wave"] = "👋",
        ["muscle"] = "💪", ["point_right"] = "👉", ["eyes"] = "👀", ["brain"] = "🧠",

        ["heart"] = "❤️", ["broken_heart"] = "💔", ["sparkling_heart"] = "💖",
        ["fire"] = "🔥", ["sparkles"] = "✨", ["star"] = "⭐", ["star2"] = "🌟",
        ["zap"] = "⚡", ["boom"] = "💥", ["tada"] = "🎉", ["confetti_ball"] = "🎊",
        ["rocket"] = "🚀", ["100"] = "💯", ["trophy"] = "🏆", ["gem"] = "💎",

        ["white_check_mark"] = "✅", ["heavy_check_mark"] = "✔️", ["ballot_box_with_check"] = "☑️",
        ["x"] = "❌", ["negative_squared_cross_mark"] = "❎", ["warning"] = "⚠️",
        ["no_entry"] = "⛔", ["question"] = "❓", ["exclamation"] = "❗",
        ["information_source"] = "ℹ️", ["bulb"] = "💡", ["lock"] = "🔒", ["key"] = "🔑",
        ["bell"] = "🔔", ["bookmark"] = "🔖", ["mag"] = "🔍", ["pushpin"] = "📌",

        ["bug"] = "🐛", ["beetle"] = "🪲", ["snake"] = "🐍", ["penguin"] = "🐧",
        ["cat"] = "🐱", ["dog"] = "🐶", ["octocat"] = "🐙", ["whale"] = "🐳",

        ["computer"] = "💻", ["package"] = "📦", ["memo"] = "📝", ["books"] = "📚",
        ["chart_with_upwards_trend"] = "📈", ["calendar"] = "📅", ["clipboard"] = "📋",
        ["wrench"] = "🔧", ["hammer"] = "🔨", ["gear"] = "⚙️", ["construction"] = "🚧",
        ["coffee"] = "☕", ["pizza"] = "🍕", ["cake"] = "🍰", ["beer"] = "🍺",
        ["earth_americas"] = "🌎", ["sunny"] = "☀️", ["moon"] = "🌙", ["rainbow"] = "🌈",
    };
}
