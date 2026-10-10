// Web 版の画面の出し方を、設定の「画面サイズ」に合わせる（2026-10-09）。
//
// 呼ぶのは SettingsManager.ApplyResolution。
//   mode 0     … 800x600 等倍。窓が小さいときは 4:3 を保ったまま縮める
//   mode 1・2 … 4:3 を保ったまま窓いっぱい（index.html の CSS に任せる）
//
// 描く解像度は 800x600 のまま（index.html の matchWebGLToCanvasSize: false）。
// ここで変えるのは、ページの上での canvas の大きさだけ。
//
// **Web 版では、全画面への切り替えをしない。「全画面」(mode 2) は「窓いっぱい」と同じに扱う。**
// 最初の版は Unity の `Screen.fullScreen` を立てていたが、全画面に入った瞬間に描く解像度が
// 80x60 ほどに落ち、マウスの指の絵だけが画面いっぱいに映った
// （2026-10-09、公開したページでユーザーが踏んだ）。
// ブラウザの全画面機能で入れ物ごと広げる版も書いたが、本物の操作でしか全画面に入れず、
// 自動操作では確かめられなかったので、入れていない。本当の全画面はブラウザの F11 で足りる。
mergeInto(LibraryManager.library, {
  KmSetCanvasMode: function (mode) {
    var canvas = document.querySelector("#unity-canvas");
    if (!canvas) return;
    if (mode === 0) {
      canvas.style.width = "min(800px, 100vw, calc(100vh * 4 / 3))";
      canvas.style.height = "min(600px, 100vh, calc(100vw * 3 / 4))";
    } else {
      // 直書きを外すと、スタイルシート（窓いっぱい）に戻る
      canvas.style.width = "";
      canvas.style.height = "";
    }
  }
});
