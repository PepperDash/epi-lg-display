namespace PepperDash.Essentials.Plugins.Lg.Display
{
    /// <summary>
    /// LG "xb" input codes that differ between panel models. Codes are kept lowercase, the form the
    /// input feedback is matched in (<c>LgDisplayController.UpdateInputFb</c> lowercases the
    /// reply); outgoing commands upper-case the hex like the rest of the driver ("xb 01 C0").
    /// Uses no Crestron types, so it is compiled straight into the tests.
    /// </summary>
    public static class LgInputCodes
    {
        /// <summary>HDMI 3 on standard LG commercial panels</summary>
        public const string Hdmi3 = "92";

        /// <summary>
        /// HDMI 3 on the LG UM5K series (verified on 110UM5K-BP.CUSFLR: "xb 01 92" answers
        /// "b 01 NG92x", and the input poll on HDMI 3 answers "b 01 OKa6x")
        /// </summary>
        public const string Hdmi3Um5kSeries = "a6";

        /// <summary>
        /// The HDMI 3 input code to send and to match feedback against
        /// </summary>
        /// <param name="um5kSeriesInputCodes">
        /// <c>LgDisplayPropertiesConfig.Um5kSeriesInputCodes</c> from the device config
        /// </param>
        public static string Hdmi3For(bool um5kSeriesInputCodes)
        {
            return um5kSeriesInputCodes ? Hdmi3Um5kSeries : Hdmi3;
        }

        /// <summary>
        /// The HDMI 3 "xb" command for set ID <paramref name="setId"/>: "xb 01 92" normally, or
        /// "xb 01 A6" for the UM5K series (hex upper-cased like "xb 01 C0" and "xb 01 FF")
        /// </summary>
        public static string Hdmi3Command(string setId, bool um5kSeriesInputCodes)
        {
            return string.Format("xb {0} {1}", setId, Hdmi3For(um5kSeriesInputCodes).ToUpperInvariant());
        }

        /// <summary>
        /// Translates an input key written with the standard codes to the key the input list and
        /// the routing ports use. With <paramref name="um5kSeriesInputCodes"/> set, the standard
        /// HDMI 3 key "92" becomes "a6", so a config or caller that names HDMI 3 as "92" still
        /// reaches HDMI 3 and the panel is sent A6, never 92. Any other key, or any key with the
        /// flag off, is returned unchanged.
        /// </summary>
        public static string NormalizeInputKey(string inputKey, bool um5kSeriesInputCodes)
        {
            return um5kSeriesInputCodes && string.Equals(inputKey, Hdmi3, System.StringComparison.OrdinalIgnoreCase)
                ? Hdmi3Um5kSeries
                : inputKey;
        }
    }
}
