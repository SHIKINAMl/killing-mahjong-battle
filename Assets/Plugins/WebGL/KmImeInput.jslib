// Web 版で日本語（IME）の文字を打てるようにする（2026-10-20）。
//
// Unity の Web 版の TMP_InputField は、変換入力（IME）を受け取れない。
// そこで、打つあいだだけ、入力欄の真上に本物の HTML の <input> を重ねる。
// 文字は HTML の側で打ってもらい、Unity が毎フレーム中身を読みに来る（WebImeInput.cs）。
//
// <input> は透明にしてある。見えている文字は Unity の入力欄が描いたもの。
// 変換候補の窓だけは、ブラウザがこの <input> の位置に出す。
//
// 位置は、Unity の画面の上での割合（左上が 0,0）で受け取り、ページの上の canvas の位置に直す。
mergeInto(LibraryManager.library, {
  // 0 … 打っている途中、1 … Enter で決めた、2 … 外をクリックするなどして離れた
  // clicked … 重ねた <input> が押されたか。押した所は Unity まで届かないので、ここで覚えて伝える（KmImeInput_ConsumeClick）
  $KmIme: { el: null, state: 0, clicked: 0 },

  KmImeInput_Open__deps: ["$KmIme"],
  KmImeInput_Open: function (x, y, w, h, textPtr, maxLength) {
    var canvas = document.querySelector("#unity-canvas");
    if (!canvas) return;
    var el = KmIme.el;
    if (!el) {
      el = document.createElement("input");
      el.type = "text";
      el.autocomplete = "off";
      el.spellcheck = false;
      var s = el.style;
      s.position = "fixed";
      s.zIndex = "10";
      s.margin = "0";
      s.padding = "0";
      s.border = "none";
      s.outline = "none";
      s.background = "transparent";
      s.color = "transparent";
      s.caretColor = "transparent";
      // Unity 側のキー操作に流さない（Enter で決めたことだけ伝える）
      el.addEventListener("keydown", function (e) {
        e.stopPropagation();
        if (e.key === "Enter" && !e.isComposing && e.keyCode !== 229) {
          e.preventDefault();
          KmIme.state = 1;
        }
      });
      el.addEventListener("keyup", function (e) { e.stopPropagation(); });
      el.addEventListener("keypress", function (e) { e.stopPropagation(); });
      el.addEventListener("pointerdown", function () { KmIme.clicked = 1; });
      el.addEventListener("blur", function () {
        if (KmIme.state === 0) KmIme.state = 2;
      });
      document.body.appendChild(el);
      KmIme.el = el;
    }
    var r = canvas.getBoundingClientRect();
    el.style.left = (r.left + x * r.width) + "px";
    el.style.top = (r.top + y * r.height) + "px";
    el.style.width = (w * r.width) + "px";
    el.style.height = (h * r.height) + "px";
    el.style.fontSize = Math.max(12, h * r.height * 0.6) + "px";
    el.maxLength = maxLength > 0 ? maxLength : 524288;
    el.value = UTF8ToString(textPtr);
    el.style.display = "block";
    KmIme.state = 0;
    el.focus();
    var n = el.value.length;
    el.setSelectionRange(n, n);
  },

  KmImeInput_GetText__deps: ["$KmIme"],
  KmImeInput_GetText: function () {
    var v = KmIme.el ? KmIme.el.value : "";
    var size = lengthBytesUTF8(v) + 1;
    var buf = _malloc(size);
    stringToUTF8(v, buf, size);
    return buf;
  },

  KmImeInput_GetState__deps: ["$KmIme"],
  KmImeInput_GetState: function () {
    return KmIme.state;
  },

  // 重ねた <input> が押されていたら 1 を返して忘れる（2026-10-22、名前の欄を押すと出るキーボードのため）
  KmImeInput_ConsumeClick__deps: ["$KmIme"],
  KmImeInput_ConsumeClick: function () {
    var c = KmIme.clicked;
    KmIme.clicked = 0;
    return c;
  },

  KmImeInput_Close__deps: ["$KmIme"],
  KmImeInput_Close: function () {
    var el = KmIme.el;
    if (!el) return;
    KmIme.state = 2;
    el.style.display = "none";
    el.blur();
    var canvas = document.querySelector("#unity-canvas");
    if (canvas) canvas.focus();
  }
});
