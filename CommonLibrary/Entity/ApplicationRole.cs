using Microsoft.AspNetCore.Identity;

namespace Dev.CommonLibrary.Entity
{
    /// <summary>
    /// ロール情報
    /// </summary>
    public class ApplicationRole : IdentityRole
    {
        public ApplicationRole() : base() { }
        public ApplicationRole(string roleName) : base(roleName) { }
    }
}
