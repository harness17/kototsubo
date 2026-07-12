using System.Text.Json;
using AutoMapper;
using Dev.CommonLibrary.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Site.Common;
using Site.Entity;
using Site.Models;
using Site.Repository;
using Site.Services;

namespace Site.Controllers
{
    /// <summary>
    /// 所蔵品コントローラー。一覧・登録・編集・詳細・削除を提供する。
    /// </summary>
    [Authorize]
    public class ItemController : Controller
    {
        private readonly ItemRepository _repository;
        private readonly IMapper _mapper;
        private readonly IBookCandidateLookupService _candidateLookupService;
        private const int PageSize = 20;

        public ItemController(
            ItemRepository repository,
            IMapper mapper,
            IBookCandidateLookupService candidateLookupService)
        {
            _repository = repository;
            _mapper = mapper;
            _candidateLookupService = candidateLookupService;
        }

        /// <summary>所蔵品一覧。</summary>
        public IActionResult Index(ItemSearchViewModel? search, int page = 1, string sort = "", string sortdir = "ASC", bool returnList = false)
        {
            search ??= new ItemSearchViewModel();

            if (returnList)
            {
                var sessionCond = TempData.Peek(SessionKey.ItemCondViewModel);
                if (sessionCond != null)
                    search = JsonSerializer.Deserialize<ItemSearchViewModel>(sessionCond.ToString()!)!;

                var sessionPage = TempData.Peek(SessionKey.ItemPageModel);
                if (sessionPage != null)
                {
                    var saved = JsonSerializer.Deserialize<PageState>(sessionPage.ToString()!)!;
                    page = saved.Page;
                    sort = saved.Sort;
                    sortdir = saved.SortDir;
                }
            }

            var cond = new ItemCondModel
            {
                Keyword = search.Keyword,
                MediaType = search.MediaType,
                IsDigital = search.IsDigital,
                OwnershipStatus = search.OwnershipStatus,
                Publisher = search.Publisher,
                ReleaseDateFrom = search.ReleaseDateFrom,
                ReleaseDateTo = search.ReleaseDateTo,
                Pager = new CommonListPagerModel(page, sort: sort, sortdir: sortdir, recoedNumber: PageSize)
            };

            if (!ModelState.IsValid)
            {
                return View(new ItemListViewModel
                {
                    Search = search,
                    Pager = cond.Pager
                });
            }

            var query = _repository.GetBaseQuery(cond);
            var totalRecords = query.Count();
            var items = query
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            var firstRecord = totalRecords == 0 ? 0 : (page - 1) * PageSize + 1;
            var endRecord = Math.Min(page * PageSize, totalRecords);

            var model = new ItemListViewModel
            {
                Items = _mapper.Map<List<ItemViewModel>>(items),
                Search = search,
                Pager = new CommonListPagerModel(page, sort: sort, sortdir: sortdir, recoedNumber: PageSize),
                Summary = new CommonListSummaryModel(
                    page, totalRecords, firstRecord, endRecord,
                    $"{totalRecords}件中 {firstRecord}～{endRecord}件を表示")
            };

            TempData[SessionKey.ItemCondViewModel] = JsonSerializer.Serialize(search);
            TempData[SessionKey.ItemPageModel] = JsonSerializer.Serialize(
                new PageState { Page = page, Sort = sort, SortDir = sortdir });

            return View(model);
        }

        /// <summary>所蔵品詳細。</summary>
        public IActionResult Details(long id)
        {
            var entity = _repository.SelectById(id);
            if (entity == null || entity.DelFlag) return NotFound();

            return View(_mapper.Map<ItemViewModel>(entity));
        }

        /// <summary>所蔵品登録画面。</summary>
        public IActionResult Create()
        {
            return View(new ItemViewModel
            {
                AcquisitionDate = DateTime.Today
            });
        }

        /// <summary>ISBN から書誌情報を取得する。</summary>
        [HttpGet]
        public async Task<IActionResult> LookupIsbn(string isbn)
        {
            if (string.IsNullOrWhiteSpace(isbn))
                return Json(new { success = false, message = "ISBNを入力してください" });

            var normalizedIsbn = OpenBDLookupService.NormalizeIsbn13(isbn);
            if (normalizedIsbn == null)
                return Json(new { success = false, message = "有効なISBNを入力してください" });

            var lookup = await _candidateLookupService.LookupCandidatesByIsbnAsync(
                normalizedIsbn);
            var candidates = lookup.Results;
            if (candidates.Count == 0)
            {
                return Json(new
                {
                    success = false,
                    message = lookup.NdlLookupFailed
                        ? "書誌情報の取得に失敗しました。しばらくしてから再度お試しください。"
                        : "該当する書籍が見つかりませんでした"
                });
            }

            var asin = OpenBDLookupService.ToAmazonAsinCandidate(normalizedIsbn);
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate.CoverImageUrl) && asin != null)
                    candidate.CoverImageUrl = OpenBDLookupService.GetAmazonCoverUrl(asin);
            }

            return Json(new { success = true, candidates, asin });
        }

        /// <summary>ASIN から書誌情報を取得する（ISBN-10互換のASINのみ対応）。</summary>
        [HttpGet]
        public async Task<IActionResult> LookupAsin(string asin)
        {
            if (string.IsNullOrWhiteSpace(asin))
                return Json(new { success = false, message = "ASINを入力してください" });

            var trimmed = asin.Trim();

            // Kindle専用ASIN（B0xxxxx）は書籍APIでは検索不可
            if (trimmed.StartsWith("B", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Kindle専用ASIN（B0...）はISBN互換ではないため検索できません" });

            // ISBN-10互換のASINをISBN-13に変換してopenBDで検索
            var isbn13 = OpenBDLookupService.NormalizeIsbn13(trimmed);
            if (isbn13 == null)
                return Json(new { success = false, message = "有効なASIN/ISBN-10形式ではありません" });

            var lookup = await _candidateLookupService.LookupCandidatesByIsbnAsync(isbn13);
            var result = lookup.Results.FirstOrDefault();
            if (result == null)
            {
                return Json(new
                {
                    success = false,
                    message = lookup.NdlLookupFailed
                        ? "書誌情報の取得に失敗しました。しばらくしてから再度お試しください。"
                        : "該当する書籍が見つかりませんでした"
                });
            }

            if (string.IsNullOrEmpty(result.CoverImageUrl))
                result.CoverImageUrl = OpenBDLookupService.GetAmazonCoverUrl(trimmed);
            return Json(new { success = true, data = result, asin = trimmed });
        }

        /// <summary>所蔵品登録処理。</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(ItemViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var entity = _mapper.Map<ItemEntity>(model);
            entity.AcquisitionDate ??= DateTime.Today;
            _repository.Insert(entity);

            TempData["Success"] = $"「{entity.Title}」を登録しました。";
            return RedirectToAction(nameof(Index), new { returnList = true });
        }

        /// <summary>所蔵品編集画面。</summary>
        public IActionResult Edit(long id)
        {
            var entity = _repository.SelectById(id);
            if (entity == null || entity.DelFlag) return NotFound();

            return View(_mapper.Map<ItemViewModel>(entity));
        }

        /// <summary>所蔵品編集処理。</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(long id, ItemViewModel model)
        {
            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid) return View(model);

            var entity = _repository.SelectById(id);
            if (entity == null || entity.DelFlag) return NotFound();

            _mapper.Map(model, entity);
            entity.AcquisitionDate ??= DateTime.Today;
            _repository.Update(entity);

            TempData["Success"] = $"「{entity.Title}」を更新しました。";
            return RedirectToAction(nameof(Index), new { returnList = true });
        }

        /// <summary>所蔵品削除確認画面。</summary>
        public IActionResult Delete(long id)
        {
            var entity = _repository.SelectById(id);
            if (entity == null || entity.DelFlag) return NotFound();

            return View(_mapper.Map<ItemViewModel>(entity));
        }

        /// <summary>所蔵品削除処理（論理削除）。</summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(long id)
        {
            var entity = _repository.SelectById(id);
            if (entity == null || entity.DelFlag) return NotFound();

            _repository.LogicalDelete(entity);

            TempData["Success"] = $"「{entity.Title}」を削除しました。";
            return RedirectToAction(nameof(Index), new { returnList = true });
        }

        private record PageState
        {
            public int Page { get; init; } = 1;
            public string Sort { get; init; } = "";
            public string SortDir { get; init; } = "ASC";
        }
    }
}
