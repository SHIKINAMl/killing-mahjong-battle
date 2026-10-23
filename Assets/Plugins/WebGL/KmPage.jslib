// Web 版のページそのものを読み込み直す（2026-10-22、展示用の「初期化」ボタン）。
//
// 呼ぶのは ExhibitionReset。名前などを消したあと、ゲームを最初から起動し直して
// サーバーのプレイヤー ID も取り直す（ID は起動している間だけ持つので、読み込み直せば新しくなる）。
mergeInto(LibraryManager.library, {
  KmReloadPage: function () {
    window.location.reload();
  },

  // このページで誰かが一度でも押したか（2026-10-23）。ブラウザは、まだ誰も押していないページに
  // 音つきの動画を流させない。タイトルの紹介動画は、押されるまでは音を消して流す（TitleRankingBoardUI）。
  // 調べる手段が無いブラウザでは「押した」とみなして、これまでどおり音つきで流す
  KmHasUserActivation: function () {
    var ua = navigator.userActivation;
    return (ua === undefined || ua.hasBeenActive) ? 1 : 0;
  }
});
