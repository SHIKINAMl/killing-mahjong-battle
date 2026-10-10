using System;
using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// **画面の部品は、コードで作るより先に「シーンに置いてある物」を使う**ための小道具（2026-10-09）。
    ///
    /// それまで、タイトルのボタン・部屋の画面・対局メニューは再生のたびにコードで組み立てていた。
    /// 再生していないとシーンに何も見えず、置き場所を直すにもコードの数字を書き換えるしかなかった
    /// （ユーザーの指示:「コードで生成するのではなく、最初から置いておくようにしてほしい」）。
    ///
    /// 使い方は「探して、無ければ作る」:
    ///
    ///     var go = SceneFirst.Child(parent, "RoomMenuBar", out bool created, typeof(RectTransform));
    ///     if (created) { /* 位置や色を決める。**シーンに置いてあった物には触らない** */ }
    ///
    /// **シーンに置いてある物が正。** 置いてあった物の位置・色・文字をコードで上書きしないこと。
    /// 上書きすると、シーンで直しても再生すると元に戻ってしまう。
    /// コードに残っている数字は、シーンに無かったときに作るための控え。
    /// 見た目を変えたいときは、シーンのほうを直す。
    ///
    /// 部品を新しく足したときは、コードに作る処理を書いたうえで
    /// `Tools > UI > 実行時UIをシーンへ置く` を実行して、シーンにも置くこと
    /// （`SceneFirstBaker`）。置き忘れると、再生したときに警告が出る。
    /// </summary>
    public static class SceneFirst
    {
        private static readonly List<string> CreatedNames = new List<string>();

        /// <summary>
        /// <paramref name="parent"/> の直下から名前で探す（非表示も含む）。無ければ作る。
        /// </summary>
        /// <param name="created">作ったら true。シーンに置いてあった物を使うなら false</param>
        public static GameObject Child(Transform parent, string name, out bool created, params Type[] components)
        {
            GameObject found = FindChild(parent, name);
            if (found != null)
            {
                created = false;
                return found;
            }

            created = true;
            var go = new GameObject(name, components);
            if (parent != null) go.transform.SetParent(parent, false);
            CreatedNames.Add(name);
            return go;
        }

        /// <summary><paramref name="parent"/> の直下から名前で探す（非表示も含む）。無ければ null。</summary>
        public static GameObject FindChild(Transform parent, string name)
        {
            if (parent == null) return null;

            // Transform.Find は名前の「/」を階層の区切りと読むので、自分で数える
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name) return child.gameObject;
            }
            return null;
        }

        /// <summary>
        /// 自前の手順で作った物を、「シーンに無かったので作った」として覚えさせる
        /// （<see cref="Child"/> を通さずに作ったとき用）。
        /// </summary>
        public static void NoteCreated(string name)
        {
            CreatedNames.Add(name);
        }

        /// <summary>
        /// シーンのどこかから名前で探す（非表示も含む）。無ければシーンの直下に作る。
        /// </summary>
        public static GameObject Root(string name, out bool created, params Type[] components)
        {
            GameObject found = Find(name);
            if (found != null)
            {
                created = false;
                return found;
            }

            created = true;
            CreatedNames.Add(name);
            return new GameObject(name, components);
        }

        /// <summary>シーンにある物を名前で探す（非表示も含む）。無ければ null。</summary>
        public static GameObject Find(string name)
        {
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var t in all)
            {
                if (t != null && t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
            }
            return null;
        }

        /// <summary>
        /// 組み立ての終わりに呼ぶ。**再生中に**作った物があれば、シーンへ置き忘れているので知らせる。
        /// </summary>
        /// <returns>作った物の名前（「、」区切り）。何も作らなかったら空文字</returns>
        public static string Report(string owner)
        {
            string made = string.Join("、", CreatedNames);
            if (CreatedNames.Count > 0 && Application.isPlaying)
            {
                Debug.LogWarning("[SceneFirst] " + owner + ": シーンに無かったので、再生時に作りました: " + made
                                 + "。Tools > UI > 実行時UIをシーンへ置く を実行すると、シーンに置かれます");
            }
            CreatedNames.Clear();
            return made;
        }
    }
}
