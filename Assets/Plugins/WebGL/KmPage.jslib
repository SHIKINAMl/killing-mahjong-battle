// Web 版のページそのものを読み込み直す（2026-10-22、展示用の「初期化」ボタン）。
//
// 呼ぶのは ExhibitionReset。名前などを消したあと、ゲームを最初から起動し直して
// サーバーのプレイヤー ID も取り直す（ID は起動している間だけ持つので、読み込み直せば新しくなる）。
mergeInto(LibraryManager.library, {
  KmReloadPage: function () {
    window.location.reload();
  }
});
