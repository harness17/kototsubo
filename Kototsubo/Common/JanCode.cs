global using Site.Common;

namespace Site.Common
{
    /// <summary>JAN-13 (EAN-13) コードの正規化と検証を行う。</summary>
    public static class JanCode
    {
        /// <summary>
        /// ハイフンと空白を除去し、チェックディジットが正しい13桁JANを返す。
        /// </summary>
        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var compact = new string(value
                .Where(c => c != '-' && !char.IsWhiteSpace(c))
                .ToArray());

            if (compact.Length != 13 || !compact.All(char.IsDigit))
                return null;

            var sum = compact
                .Take(12)
                .Select((c, index) => (c - '0') * (index % 2 == 0 ? 1 : 3))
                .Sum();
            var checkDigit = (10 - sum % 10) % 10;

            return checkDigit == compact[12] - '0' ? compact : null;
        }
    }
}
