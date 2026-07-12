using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;

namespace Dev.CommonLibrary.Attributes
{
    /// <summary>
    /// ファイルサイズ検証属性
    /// </summary>
    public class FileSizeAttribute : ValidationAttribute
    {
        public long MaxSize { get; set; }
        public Type? ResourceType { get; set; }
        public string? ResourceName { get; set; }

        public FileSizeAttribute(long MaxSize = 1024 * 1024 * 2)
        {
            this.MaxSize = MaxSize;
        }

        public override bool IsValid(object? value)
        {
            if (value == null) return true;
            if (value is IFormFile file)
                return file.Length <= MaxSize;
            if (value is IFormFile[] files)
                return files.All(f => f == null || f.Length <= MaxSize);
            return true;
        }
    }

    /// <summary>
    /// ファイル種別検証属性
    /// </summary>
    public class FileTypesAttribute : ValidationAttribute
    {
        private readonly string[] _types;
        public string types { get; set; } = "";
        public Type? ResourceType { get; set; }
        public string? ResourceName { get; set; }

        public FileTypesAttribute(string types = "")
        {
            this.types = types;
            _types = types.Split(',').Select(t => t.Trim().ToLower()).ToArray();
        }

        public override bool IsValid(object? value)
        {
            if (value == null) return true;
            if (value is IFormFile file)
                return IsValidFile(file);
            if (value is IFormFile[] files)
                return files.All(f => f == null || IsValidFile(f));
            return true;
        }

        private bool IsValidFile(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLower();
            return _types.Contains(ext);
        }
    }
}
