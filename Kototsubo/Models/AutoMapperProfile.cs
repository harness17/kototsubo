using AutoMapper;
using Site.Entity;

namespace Site.Models
{
    /// <summary>
    /// AutoMapper プロファイル。Entity ↔ ViewModel の変換を定義する。
    /// </summary>
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<ItemEntity, ItemViewModel>().ReverseMap();
            CreateMap<WordEntity, WordViewModel>()
                .ReverseMap()
                .ForMember(entity => entity.UserId, options => options.Ignore());
        }
    }
}
