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
  // 調べる手段が無いブラウザでは「押した」とみなして、これまでどおり音つきで流す。
  //
  // まだ誰も押していなくても、ブラウザが音を許しているなら 1 を返す（2026-10-24。ユーザーから
  // 「ゲーム初回起動時のみ動画の音声が流れない」）。展示の PC で Chrome を
  // --autoplay-policy=no-user-gesture-required で起動しておけば、起動直後から音が出る。
  // 見分け方は2つ: 自動再生の決まりを尋ねる（getAutoplayPolicy。無いブラウザもある）か、
  // ゲームの音の出口（WEBAudio の AudioContext）が止められずに動いているか
  KmHasUserActivation: function () {
    var ua = navigator.userActivation;
    if (ua === undefined || ua.hasBeenActive) return 1;
    try {
      if (navigator.getAutoplayPolicy && navigator.getAutoplayPolicy("mediaelement") === "allowed") return 1;
    } catch (e) {}
    try {
      if (typeof WEBAudio !== "undefined" && WEBAudio.audioContext && WEBAudio.audioContext.state === "running") return 1;
    } catch (e) {}
    return 0;
  }
});
