using Microsoft.AspNetCore.Identity;

namespace Dev.CommonLibrary.Entity
{
    /// <summary>
    /// アカウント情報
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        public ApplicationUser() : base()
        {
            PreviousUserPasswords = new List<UserPreviousPassword>();
        }

        public virtual IList<UserPreviousPassword> PreviousUserPasswords { get; set; }
        public DateTime? ResetPasswordTimeOut { get; set; }
        public DateTime? PasswordAvailableEndDate { get; set; }
        public string? ApplicationRoleName { get; set; }
        public string? UpdateApplicationUserId { get; set; }
    }
}
