using Microsoft.AspNetCore.Http;

namespace Dev.CommonLibrary.Entity
{
    /// <summary>
    /// エンティティベースクラス
    /// </summary>
    public abstract class EntityBase : IEntity
    {
        // IHttpContextAccessorはDIで設定する
        public static IHttpContextAccessor? HttpContextAccessor { get; set; }

        public bool DelFlag { get; set; }

        public string? UpdateApplicationUserId { get; set; }
        public string? CreateApplicationUserId { get; set; }
        public DateTime UpdateDate { get; set; }
        public DateTime CreateDate { get; set; }

        private string? GetCurrentUserId()
        {
            return HttpContextAccessor?.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }

        /// <summary>新規登録時の監査カラム（作成者・作成日時・更新者・更新日時）を設定する。</summary>
        public void SetForCreate()
        {
            var userId = GetCurrentUserId();
            CreateApplicationUserId = userId ?? (string.IsNullOrEmpty(CreateApplicationUserId) ? string.Empty : CreateApplicationUserId);
            UpdateApplicationUserId = userId ?? (string.IsNullOrEmpty(UpdateApplicationUserId) ? string.Empty : UpdateApplicationUserId);
            CreateDate = DateTime.Now;
            UpdateDate = CreateDate;
        }

        /// <summary>更新時の監査カラム（更新者・更新日時）を設定する。</summary>
        public void SetForUpdate()
        {
            var userId = GetCurrentUserId();
            UpdateApplicationUserId = userId ?? (string.IsNullOrEmpty(UpdateApplicationUserId) ? string.Empty : UpdateApplicationUserId);
            UpdateDate = DateTime.Now;
        }

        /// <summary>論理削除フラグを立て、更新監査カラムを設定する。</summary>
        public void SetForLogicalDelete()
        {
            DelFlag = true;
            SetForUpdate();
        }
    }
}
