// フォームにdata-loading-text属性があれば送信時にローディングオーバーレイを表示する
$(function () {
    var $overlay = $('#loading-overlay');
    if (!$overlay.length) return;

    $(document).on('submit', 'form[data-loading-text]', function () {
        $overlay.find('.loading-text').text($(this).data('loading-text'));
        $overlay.addClass('active');
    });
});
