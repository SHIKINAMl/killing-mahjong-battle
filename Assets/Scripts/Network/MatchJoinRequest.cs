namespace KillingMahjong.Network
{
    /// <summary>
    /// マルチ対戦の入り方。サーバーの `join` の `mode` と対になっている
    /// （`websocket_server.py` の `_handle_join_request`、2026-08-03 の `addf82e` で追加）。
    /// </summary>
    public enum MatchJoinMode
    {
        /// <summary>野良。待機列に並んで、2人揃った順に組まされる。</summary>
        Public,

        /// <summary>部屋を作る。サーバーが5文字の合言葉を発行し、相手が来るまで待つ。</summary>
        PrivateCreate,

        /// <summary>合言葉を打ち込んで、相手の部屋へ入る。</summary>
        PrivateJoin,
    }

    /// <summary>
    /// 「次に繋いだとき、どうやって対戦相手を探すか」の置き場。
    ///
    /// **接続してから選ぶのでは間に合わない。** `join` はサーバーの `connected` を受けた直後に
    /// 送るため（`WebSocketGameClientSample`）、対局シーンへ移る前に決めておく必要がある。
    /// タイトル側でここへ入れてからシーンを切り替える、という使い方をする。
    ///
    /// 静的にしているのはシーンをまたいで持ち越すため。対局シーンが2つあるので、
    /// どちらのシーンにも置かなくて済むこの形にしている。
    /// </summary>
    public static class MatchJoinRequest
    {
        /// <summary>合言葉の文字数。サーバーの `PRIVATE_ROOM_PASSWORD_LENGTH` と揃えること。</summary>
        public const int PasswordLength = 5;

        public static MatchJoinMode Mode { get; private set; } = MatchJoinMode.Public;

        /// <summary><see cref="MatchJoinMode.PrivateJoin"/> のときだけ使う合言葉。</summary>
        public static string Password { get; private set; } = "";

        /// <summary>野良で探す。</summary>
        public static void RequestPublic()
        {
            Mode = MatchJoinMode.Public;
            Password = "";
        }

        /// <summary>部屋を作る。合言葉はサーバーが決めて `matching_waiting` で返してくる。</summary>
        public static void RequestPrivateCreate()
        {
            Mode = MatchJoinMode.PrivateCreate;
            Password = "";
        }

        /// <summary>合言葉で相手の部屋に入る。小文字で渡されても通るよう大文字に直す。</summary>
        public static void RequestPrivateJoin(string password)
        {
            Mode = MatchJoinMode.PrivateJoin;
            Password = Normalize(password);
        }

        /// <summary>入力欄の検証用。サーバーと同じ条件（英大文字と数字ちょうど5文字）。</summary>
        public static bool IsValidPassword(string password)
        {
            string normalized = Normalize(password);
            if (normalized.Length != PasswordLength) return false;

            foreach (char c in normalized)
            {
                bool isUpper = c >= 'A' && c <= 'Z';
                bool isDigit = c >= '0' && c <= '9';
                if (!isUpper && !isDigit) return false;
            }
            return true;
        }

        private static string Normalize(string password)
        {
            if (string.IsNullOrEmpty(password)) return "";
            return password.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// サーバーへ送る `join` の JSON を作る。
        /// 野良のときも `mode` を明示している（サーバーの既定も public だが、
        /// ログを見たときにどちらで入ったのか分かるようにするため）。
        /// </summary>
        public static string BuildJoinJson()
        {
            switch (Mode)
            {
                case MatchJoinMode.PrivateCreate:
                    return "{\"type\":\"join\",\"data\":{\"mode\":\"private_create\"}}";

                case MatchJoinMode.PrivateJoin:
                    return "{\"type\":\"join\",\"data\":{\"mode\":\"private_join\",\"password\":\"" + Password + "\"}}";

                default:
                    return "{\"type\":\"join\",\"data\":{\"mode\":\"public\"}}";
            }
        }
    }
}
