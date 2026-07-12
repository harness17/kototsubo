using System.Security.Claims;
using AutoMapper;
using Dev.CommonLibrary.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Site.Common;
using Site.Entity;
using Site.Models;
using Site.Repository;

namespace Site.Controllers
{
    /// <summary>
    /// 収集した言葉の一覧・登録・編集・詳細・削除を提供する。
    /// </summary>
    [Authorize]
    public class WordController : Controller
    {
        private const int PageSize = 20;
        private readonly WordRepository _repository;
        private readonly ItemRepository _itemRepository;
        private readonly IMapper _mapper;

        public WordController(
            WordRepository repository,
            ItemRepository itemRepository,
            IMapper mapper)
        {
            _repository = repository;
            _itemRepository = itemRepository;
            _mapper = mapper;
        }

        /// <summary>言葉一覧。</summary>
        public IActionResult Index(
            WordSearchViewModel search,
            long? itemId,
            int page = 1,
            string sort = "",
            string sortdir = "ASC")
        {
            page = Math.Max(1, page);
            var cond = new WordCondModel
            {
                Keyword = search.Keyword,
                Genre = search.Genre,
                ItemId = itemId,
                Pager = new CommonListPagerModel(
                    page,
                    sort: sort,
                    sortdir: sortdir,
                    recoedNumber: PageSize)
            };

            var query = _repository.GetBaseQuery(cond);
            var totalRecords = query.Count();
            var entities = query
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            var models = _mapper.Map<List<WordViewModel>>(entities);
            SetItemTitles(models);

            var firstRecord = totalRecords == 0 ? 0 : (page - 1) * PageSize + 1;
            var endRecord = Math.Min(page * PageSize, totalRecords);
            var item = itemId.HasValue ? GetActiveItem(itemId.Value) : null;

            return View(new WordListViewModel
            {
                Items = models,
                Search = search,
                ItemId = itemId,
                ItemTitle = item?.Title,
                Pager = cond.Pager,
                Summary = new CommonListSummaryModel(
                    page,
                    totalRecords,
                    firstRecord,
                    endRecord,
                    $"{totalRecords}件中 {firstRecord}～{endRecord}件を表示")
            });
        }

        /// <summary>言葉詳細。</summary>
        public IActionResult Details(long id)
        {
            var entity = GetActiveWord(id);
            if (entity == null) return NotFound();

            var model = _mapper.Map<WordViewModel>(entity);
            SetItemTitle(model);
            return View(model);
        }

        /// <summary>言葉登録画面。</summary>
        public IActionResult Create(long? itemId)
        {
            var model = new WordViewModel { ItemId = itemId };
            if (itemId.HasValue)
            {
                var item = GetActiveItem(itemId.Value);
                if (item == null) return NotFound();

                model.ItemTitle = item.Title;
                model.SourceTitle = item.Title;
                model.Author = item.Creator;
                model.Genre = ToWordGenre(item.MediaType);
            }

            return View(model);
        }

        /// <summary>言葉登録・編集画面から関連付ける所蔵品を検索する。</summary>
        [HttpGet]
        [Authorize]
        public IActionResult SearchItems(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return Json(Array.Empty<object>());

            var items = _itemRepository.SearchForWordAssociation(keyword)
                .Select(x => new
                {
                    id = x.Id,
                    title = x.Title,
                    creator = x.Creator,
                    mediaType = (int)x.MediaType
                })
                .ToList();

            return Json(items);
        }

        /// <summary>言葉登録処理。</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(WordViewModel model)
        {
            ValidateItem(model.ItemId);
            if (!ModelState.IsValid)
            {
                SetItemTitle(model);
                return View(model);
            }

            var entity = _mapper.Map<WordEntity>(model);
            entity.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _repository.Insert(entity);

            TempData["Success"] = "言葉を登録しました。";
            return RedirectAfterSave(entity.ItemId);
        }

        /// <summary>言葉編集画面。</summary>
        public IActionResult Edit(long id)
        {
            var entity = GetActiveWord(id);
            if (entity == null) return NotFound();

            var model = _mapper.Map<WordViewModel>(entity);
            SetItemTitle(model);
            return View(model);
        }

        /// <summary>言葉編集処理。</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(long id, WordViewModel model)
        {
            if (id != model.Id) return BadRequest();

            ValidateItem(model.ItemId);
            if (!ModelState.IsValid)
            {
                SetItemTitle(model);
                return View(model);
            }

            var entity = GetActiveWord(id);
            if (entity == null) return NotFound();

            _mapper.Map(model, entity);
            _repository.Update(entity);

            TempData["Success"] = "言葉を更新しました。";
            return RedirectAfterSave(entity.ItemId);
        }

        /// <summary>言葉削除確認画面。</summary>
        public IActionResult Delete(long id)
        {
            var entity = GetActiveWord(id);
            if (entity == null) return NotFound();

            var model = _mapper.Map<WordViewModel>(entity);
            SetItemTitle(model);
            return View(model);
        }

        /// <summary>言葉削除処理（論理削除）。</summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(long id)
        {
            var entity = GetActiveWord(id);
            if (entity == null) return NotFound();

            var itemId = entity.ItemId;
            _repository.LogicalDelete(entity);

            TempData["Success"] = "言葉を削除しました。";
            return RedirectAfterSave(itemId);
        }

        private WordEntity? GetActiveWord(long id)
        {
            var entity = _repository.SelectById(id);
            return entity is { DelFlag: false } ? entity : null;
        }

        private ItemEntity? GetActiveItem(long id)
        {
            var entity = _itemRepository.SelectById(id);
            return entity is { DelFlag: false } ? entity : null;
        }

        private void ValidateItem(long? itemId)
        {
            if (itemId.HasValue && GetActiveItem(itemId.Value) == null)
                ModelState.AddModelError(nameof(WordViewModel.ItemId), "関連する所蔵品が見つかりません。");
        }

        private void SetItemTitles(IEnumerable<WordViewModel> models)
        {
            foreach (var model in models)
                SetItemTitle(model);
        }

        private void SetItemTitle(WordViewModel model)
        {
            if (model.ItemId.HasValue)
                model.ItemTitle = GetActiveItem(model.ItemId.Value)?.Title;
        }

        private IActionResult RedirectAfterSave(long? itemId)
        {
            return itemId.HasValue
                ? RedirectToAction(nameof(Index), new { itemId })
                : RedirectToAction(nameof(Index));
        }

        private static WordGenre? ToWordGenre(MediaType mediaType)
        {
            return mediaType switch
            {
                MediaType.Book => WordGenre.Book,
                MediaType.Movie => WordGenre.Movie,
                MediaType.Music => WordGenre.Music,
                MediaType.Game => WordGenre.Game,
                _ => WordGenre.Other
            };
        }
    }
}
