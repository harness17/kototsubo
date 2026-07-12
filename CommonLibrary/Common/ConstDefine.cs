namespace Dev.CommonLibrary.Common
{
    /// <summary>
    /// 正規表現定数
    /// </summary>
    public static class ConstRegExpr
    {
        public const string MailAddress = @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)*$";
        public const string PhoneNumber = @"^0\d{9,10}$";
        public const string PostalCode = @"^\d{3}-?\d{4}$";
        public const string InValidFileName = @"(CON|PRN|AUX|CLOCK\$|NUL|COM[1-9]|LPT[1-9])(\.|$)";
    }
}
