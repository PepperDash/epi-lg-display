using System.Text.RegularExpressions;

namespace PepperDash.Essentials.Plugins.Lg.Display
{
    /// <summary>
    /// One reply from the panel: "{command} {set id} {OK|NG}{data}x", for example "a 01 OK01x".
    /// </summary>
    public sealed class LgResponse
    {
        public LgResponse(string command, string setId, bool isOk, string data)
        {
            Command = command;
            SetId = setId;
            IsOk = isOk;
            Data = data;
        }

        /// <summary>The command's second letter: "a" power, "b" input, "f" volume, "e" mute, "d" video mute</summary>
        public string Command { get; private set; }

        public string SetId { get; private set; }

        /// <summary>True for OK, false for NG</summary>
        public bool IsOk { get; private set; }

        /// <summary>Hex data after OK/NG, without the trailing "x"</summary>
        public string Data { get; private set; }
    }

    /// <summary>
    /// Finds the panel's reply in one gathered line.
    /// </summary>
    /// <remarks>
    /// Replies are gathered on the "x" delimiter. Some panels also send unsolicited lines with no
    /// "x" (for example "mc 01 ea" / "mc 01 eb" around power changes) and stray bytes while powering
    /// up or down. Those end up in front of the next reply in the same gathered line, so the reply
    /// is taken from the end of the line and anything before it is ignored.
    /// </remarks>
    public static class LgResponseParser
    {
        // Command letter, set ID (1-2 hex digits), OK/NG, data, optional "x" delimiter, end of line
        private static readonly Regex ReplyAtEnd = new Regex(
            @"(?<cmd>[a-z]) (?<id>[0-9A-Fa-f]{1,2}) (?<status>OK|NG)(?<data>[0-9A-Za-z]*?)x?\s*\z",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Parses the reply at the end of <paramref name="line"/>. Returns null when the line has no reply.
        /// </summary>
        public static LgResponse Parse(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;

            var match = ReplyAtEnd.Match(line);
            if (!match.Success) return null;

            return new LgResponse(
                match.Groups["cmd"].Value,
                match.Groups["id"].Value,
                match.Groups["status"].Value == "OK",
                match.Groups["data"].Value);
        }

        /// <summary>
        /// True when <paramref name="line"/> has anything other than whitespace before the reply,
        /// such as an unsolicited "mc" line or stray bytes.
        /// </summary>
        public static bool HasLeadingNoise(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;

            var match = ReplyAtEnd.Match(line);
            return match.Success && line.Substring(0, match.Index).Trim().Length > 0;
        }
    }
}
