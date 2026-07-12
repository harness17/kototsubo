using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dev.CommonLibrary.Entity
{
    /// <summary>
    /// 以前使用したパスワード履歴
    /// </summary>
    public class UserPreviousPassword
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ApplicationUserId { get; set; } = "";

        [Required]
        public string PasswordHash { get; set; } = "";

        public DateTime CreateDate { get; set; }

        [ForeignKey("ApplicationUserId")]
        public virtual ApplicationUser? ApplicationUser { get; set; }
    }
}
