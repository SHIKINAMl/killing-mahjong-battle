using System.Collections.Generic;

namespace KillingMahjong.Network
{
    /// <summary>
    /// サーバーメッセージ種別ごとの処理を担うハンドラのインターフェース。
    ///
    /// NetworkMessageHandler は受信JSONの type を見て、該当する MessageTypes を
    /// 持つハンドラへ処理を委譲する。1つのハンドラが関連する複数の type を
    /// まとめて扱ってもよい（例: dealing_started / dealing_completed）。
    ///
    /// 実装クラスは MonoBehaviour ではないプレーンなC#クラスとし、
    /// イベントの発火は network 引数の Raise 系メソッドを通して行うこと。
    /// </summary>
    public interface IServerMessageHandler
    {
        /// <summary>このハンドラが処理するメッセージ type の一覧</summary>
        IReadOnlyList<string> MessageTypes { get; }

        /// <summary>メッセージを処理する</summary>
        /// <param name="messageType">受信メッセージの type</param>
        /// <param name="jsonString">受信JSON全文</param>
        /// <param name="network">イベント発火・LocalPlayerId 参照用のコンテキスト</param>
        void Handle(string messageType, string jsonString, NetworkMessageHandler network);
    }
}
