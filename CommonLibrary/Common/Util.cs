using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Dev.CommonLibrary.Common
{
    /// <summary>
    /// 共通関数クラス
    /// </summary>
    public static class Util
    {
        /// <summary>文字列の MD5 ハッシュ値を小文字 16 進数で返す。</summary>
        public static string CalcMd5(string srcStr)
        {
            byte[] srcBytes = Encoding.UTF8.GetBytes(srcStr);
            byte[] destBytes = MD5.HashData(srcBytes);
            var sb = new StringBuilder();
            foreach (byte b in destBytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>ファイルパスからファイル名部分だけを取り出して返す（バックスラッシュ区切り）。</summary>
        public static string SetFileName(string strFileName)
        {
            string targettext = "\\";
            if (strFileName.Contains(targettext))
            {
                int lastindex = strFileName.LastIndexOf(targettext);
                strFileName = strFileName.Substring(lastindex + targettext.Length);
            }
            return strFileName;
        }

        /// <summary>パストラバーサルや無効文字を含まない安全なパスか検証する。isFileName=true のときファイル名として検証する。</summary>
        public static bool IsSafePath(string path, bool isFileName)
        {
            if (string.IsNullOrEmpty(path)) return false;
            // パストラバーサル（..）を含む場合は拒否
            if (path.Contains("..")) return false;
            char[] invalidChars = isFileName
                ? Path.GetInvalidFileNameChars()
                : Path.GetInvalidPathChars();
            if (path.IndexOfAny(invalidChars) >= 0) return false;
            if (Regex.IsMatch(path, ConstRegExpr.InValidFileName, RegexOptions.IgnoreCase)) return false;
            return true;
        }

        /// <summary>ページャー情報と総件数からページ概要モデルを生成する。totalRecords=0 のときは 0〜0 件を返す。</summary>
        public static CommonListSummaryModel CreateSummary(CommonListPagerModel pager, int totalRecords, string listSummaryFormat)
        {
            int pageIndex = pager.page - 1;
            // totalRecords が 0 の場合は 0〜0 を返す（1〜10 のような不正な表示を防ぐ）
            int firstRecord = totalRecords > 0 ? (pageIndex * pager.recoedNumber) + 1 : 0;
            int endRecord = totalRecords > 0 ? Math.Min(firstRecord - 1 + pager.recoedNumber, totalRecords) : 0;
            string summary = string.Format(listSummaryFormat, totalRecords, firstRecord, endRecord);
            return new CommonListSummaryModel(pager.page, totalRecords, firstRecord, endRecord, summary);
        }
    }
}
